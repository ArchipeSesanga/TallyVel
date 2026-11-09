using System.Security.Cryptography;
using System.Text;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

public sealed record ArrearLine(Guid MemberUserId, string FullName, long ShortfallCents);

/// <summary>
/// Who owes what for a cycle, counted up to <see cref="AsOfDate"/>. <see cref="Version"/> identifies EXACTLY the inputs
/// of this calculation (see <see cref="CycleClosing.CalculateArrearsAsync"/>): if anything that affects the result changes,
/// the version changes, so a close that carries an old version can be refused.
/// </summary>
public sealed record ArrearsCalculation(
    Guid CycleId,
    string CycleLabel,
    DateOnly AsOfDate,
    IReadOnlyList<ArrearLine> Arrears,
    long TotalArrearsCents,
    string Version);

public enum CloseStatus { Closed, AlreadyClosed, Stale }

public sealed record CloseOutcome(CloseStatus Status, DateTimeOffset? ClosedAt, ArrearsCalculation? Calculation);

/// <summary>
/// The ONE place the arrears of a cycle are worked out and a cycle is closed. The 6.4 job (CycleCloser), the
/// close preview and the treasurer's close all call this, so the preview can never disagree with the real thing.
/// </summary>
public static class CycleClosing
{
    /// <summary>
    /// A cycle is counted up to its last day, or up to today when it is still running (an early close).
    /// Members who joined before the end of that day owe the contribution amount minus what they paid
    /// (Paid and Confirmed contributions only, the same rule as everywhere else).
    ///
    /// VERSION: the SHA-256 (hex) of a text made of the inputs, in a fixed order:
    ///   "v1|{cycleId}|{label}|{asOfDate}|{contributionAmountCents}" then, for every member counted, ordered by user id,
    ///   "|{userId}:{paidCents}".
    /// It therefore changes when a contribution that counts is added, removed, edited or changes status; when a member
    /// joins or leaves the counted group; when the amount owed per member changes; or when the as-of date moves.
    /// It does NOT change for things that cannot change the result (a Pending contribution that still does not count).
    /// </summary>
    public static async Task<ArrearsCalculation> CalculateArrearsAsync(
        TallyVelDbContext db, Guid cycleId, DateOnly today, CancellationToken ct)
    {
        var cycle = await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId, ct);
        var contributionAmount = await db.Stokvels.AsNoTracking()
            .Where(s => s.Id == cycle.StokvelId)
            .Select(s => s.ContributionAmount)
            .SingleAsync(ct);

        var asOf = cycle.EndsOn is { } endsOn && endsOn < today ? endsOn : today;
        var closesAt = StokvelCalendar.ClosesAt(asOf);
        var stokvelId = cycle.StokvelId;
        var label = cycle.Label;

        // Members who had joined before the end of the as-of day, with what each paid, in one query.
        var members = await db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.StokvelId == stokvelId && m.JoinedAt < closesAt)
            .Select(m => new
            {
                m.UserId,
                m.User.FullName,
                Paid = db.Contributions
                    .Where(ContributionRules.Counts)
                    .Where(c => c.StokvelId == stokvelId && c.MemberUserId == m.UserId && c.Cycle == label)
                    .Sum(c => (decimal?)c.Amount) ?? 0m,
            })
            .ToListAsync(ct);

        var dueCents = Money.ToCents(contributionAmount);
        var arrears = members
            .Select(m => new ArrearLine(m.UserId, m.FullName, dueCents - Money.ToCents(m.Paid)))
            .Where(a => a.ShortfallCents > 0)
            .OrderBy(a => a.FullName).ThenBy(a => a.MemberUserId)
            .ToList();

        var inputs = new StringBuilder($"v1|{cycleId}|{label}|{asOf:yyyy-MM-dd}|{dueCents}");
        foreach (var m in members.OrderBy(m => m.UserId))
            inputs.Append('|').Append(m.UserId).Append(':').Append(Money.ToCents(m.Paid));
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs.ToString()))).ToLowerInvariant();

        return new ArrearsCalculation(cycleId, label, asOf, arrears, arrears.Sum(a => a.ShortfallCents), version);
    }

    /// <summary>
    /// Closes a cycle exactly once. The close is CLAIMED with a conditional UPDATE (ClosedAt IS NULL) and the row count says
    /// who won; the arrears rows and the next cycle are written in the same transaction. If <paramref name="expectedVersion"/>
    /// is given and the calculation no longer matches it, nothing is closed (the transaction is rolled back).
    /// </summary>
    public static async Task<CloseOutcome> CloseAsync(
        TallyVelDbContext db, Guid cycleId, DateTimeOffset now, string? expectedVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // CLAIM: only one caller can flip ClosedAt from NULL; a second one waits for the first, then matches no row.
        var claimed = await db.ContributionCycles
            .Where(c => c.Id == cycleId && c.ClosedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAt, now), ct);
        if (claimed == 0)
            return new CloseOutcome(CloseStatus.AlreadyClosed, null, null);

        // Worked out AFTER the claim and inside the transaction, so what is checked is what is written.
        var calculation = await CalculateArrearsAsync(db, cycleId, StokvelCalendar.Today(now), ct);
        if (expectedVersion is not null && !string.Equals(expectedVersion, calculation.Version, StringComparison.Ordinal))
        {
            await tx.RollbackAsync(ct); // un-claims the cycle: nothing was closed
            return new CloseOutcome(CloseStatus.Stale, null, calculation);
        }

        var cycle = await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId, ct);
        var stokvelId = cycle.StokvelId;

        db.Arrears.AddRange(calculation.Arrears.Select(a => new Arrear(stokvelId, cycleId, a.MemberUserId, a.ShortfallCents, now)));

        // Open the next cycle unless one already covers it (only a cycle with dates can say what comes next).
        if (cycle.EndsOn is { } endsOn)
        {
            var frequency = await db.Stokvels.AsNoTracking()
                .Where(s => s.Id == stokvelId).Select(s => s.Cycle).SingleAsync(ct);
            var nextStart = endsOn.AddDays(1);
            var nextLabel = StokvelCalendar.LabelFor(nextStart, frequency);
            var nextExists = await db.ContributionCycles.AnyAsync(c =>
                c.StokvelId == stokvelId && (c.StartsOn >= nextStart || c.Label == nextLabel), ct);
            if (!nextExists)
                db.ContributionCycles.Add(new ContributionCycle(
                    stokvelId, nextLabel, nextStart, StokvelCalendar.EndOfCycle(nextStart, frequency)));
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CloseOutcome(CloseStatus.Closed, now, calculation);
    }
}
