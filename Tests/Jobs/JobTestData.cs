using Domain;
using Microsoft.EntityFrameworkCore;
using Tests.TestSupport;

namespace Tests.Jobs;

/// <summary>
/// Inserts job-test data straight into the database and reads it back.
/// Members join "now" (real time), so tests start cycles today or later and
/// move the fake clock past their end; see CycleCloseJobTests.
/// </summary>
public sealed class JobTestData(PostgresApiFactory factory)
{
    public sealed record Seeded(Guid StokvelId, Guid CycleId, string Label, IReadOnlyList<Guid> UserIds);

    public sealed record CycleRow(Guid Id, string Label, DateOnly? StartsOn, DateOnly? EndsOn, DateTimeOffset? ClosedAt);

    public sealed record ArrearRow(Guid CycleId, Guid MemberUserId, long ShortfallCents);

    public static DateOnly RealToday() => StokvelCalendar.Today(TimeProvider.System.GetUtcNow());

    /// <summary>Users, a stokvel they all belong to, one dated cycle, and contributions (member index -> amount).</summary>
    public async Task<Seeded> SeedAsync(
        int members, decimal amount, ContributionFrequency frequency,
        DateOnly startsOn, DateOnly endsOn, IReadOnlyDictionary<int, decimal>? paid = null)
    {
        await using var db = factory.CreateContext();

        var users = Enumerable.Range(0, members)
            .Select(i => new User($"{Guid.NewGuid():N}@job.test", $"Job Test {i}", "hash"))
            .ToList();
        db.Users.AddRange(users);

        var stokvel = new Stokvel($"Job Test {Guid.NewGuid():N}", amount, frequency, users[0].Id);
        foreach (var u in users.Skip(1))
            stokvel.AddMember(u.Id);
        db.Stokvels.Add(stokvel);

        var label = StokvelCalendar.LabelFor(startsOn, frequency);
        var cycle = new ContributionCycle(stokvel.Id, label, startsOn, endsOn);
        db.ContributionCycles.Add(cycle);

        foreach (var (index, value) in paid ?? new Dictionary<int, decimal>())
            db.Contributions.Add(new Contribution(stokvel.Id, users[index].Id, label, value));

        await db.SaveChangesAsync();
        return new Seeded(stokvel.Id, cycle.Id, label, users.Select(u => u.Id).ToList());
    }

    public async Task<List<CycleRow>> CyclesAsync(Guid stokvelId)
    {
        await using var db = factory.CreateContext();
        return await db.ContributionCycles.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.StartsOn)
            .Select(c => new CycleRow(c.Id, c.Label, c.StartsOn, c.EndsOn, c.ClosedAt))
            .ToListAsync();
    }

    public async Task<List<ArrearRow>> ArrearsAsync(Guid stokvelId)
    {
        await using var db = factory.CreateContext();
        return await db.Arrears.AsNoTracking()
            .Where(a => a.StokvelId == stokvelId)
            .OrderBy(a => a.CycleId).ThenBy(a => a.MemberUserId)
            .Select(a => new ArrearRow(a.CycleId, a.MemberUserId, a.ShortfallCents))
            .ToListAsync();
    }

    public async Task<DateTimeOffset?> ClosedAtAsync(Guid cycleId)
    {
        await using var db = factory.CreateContext();
        return await db.ContributionCycles.AsNoTracking()
            .Where(c => c.Id == cycleId)
            .Select(c => c.ClosedAt)
            .SingleAsync();
    }

    public async Task AddArrearAsync(Seeded s, int memberIndex, long shortfallCents)
    {
        await using var db = factory.CreateContext();
        db.Arrears.Add(new Arrear(s.StokvelId, s.CycleId, s.UserIds[memberIndex], shortfallCents, TimeProvider.System.GetUtcNow()));
        await db.SaveChangesAsync();
    }
}
