using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Domain;

namespace Infrastructure.Persistence;

/// <summary>Stores contribution cycles and their payouts in PostgreSQL. Scoped, like the other repositories.</summary>
public sealed class EfCycleRepository(TallyVelDbContext db) : ICycleRepository
{
    public async Task<IReadOnlyList<ContributionCycle>> ListAsync(Guid stokvelId, CancellationToken ct) =>
        await db.ContributionCycles
            .AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.Label)
            .ToListAsync(ct);

    public async Task<ContributionCycle> OpenAsync(Guid stokvelId, string label, CancellationToken ct)
    {
        var cycle = new ContributionCycle(stokvelId, label);
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync(ct);
        return cycle;
    }

    public async Task<Payout> PayOutAsync(Guid stokvelId, Guid cycleId, Guid recipientUserId, CancellationToken ct)
    {
        var cycle = await db.ContributionCycles
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
            ?? throw new NotFoundException("cycle", cycleId);

        if (!await db.StokvelMembers.AnyAsync(m => m.StokvelId == stokvelId && m.UserId == recipientUserId, ct))
            throw new BusinessRuleViolationException(
                "recipient-not-member", $"User {recipientUserId} is not a member of this stokvel.");

        var total = await db.Contributions
            .Where(c => c.StokvelId == stokvelId && c.Cycle == cycle.Label)
            .SumAsync(c => c.Amount, ct);

        if (total <= 0)
            throw new BusinessRuleViolationException(
                "nothing-to-pay-out", $"No contributions have been recorded for cycle '{cycle.Label}'.");

        try
        {
            cycle.MarkPaidOut();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleViolationException("cycle-already-paid-out", ex.Message);
        }

        var payout = new Payout(stokvelId, cycle.Id, recipientUserId, total);
        db.Payouts.Add(payout);

        // One SaveChanges is one transaction: the payout and the closed cycle land together.
        await db.SaveChangesAsync(ct);
        return payout;
    }
}
