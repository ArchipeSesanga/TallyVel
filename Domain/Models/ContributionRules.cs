using System.Linq.Expressions;

namespace Domain;

/// <summary>
/// The one place that decides which contributions count towards a member's
/// paid and owed amounts for a cycle (home screen), towards a cycle's arrears
/// (cycle-close job) and towards its payout total.
/// Paid and Confirmed count. Pending does not: the money has not arrived yet.
/// </summary>
public static class ContributionRules
{
    /// <summary>The rule as an expression, so it can sit inside a database query.</summary>
    public static readonly Expression<Func<Contribution, bool>> Counts =
        c => c.Status == ContributionStatus.Paid || c.Status == ContributionStatus.Confirmed;

    private static readonly Func<Contribution, bool> CountsInMemory = Counts.Compile();

    /// <summary>The same rule, for a contribution already in memory.</summary>
    public static bool IsCounted(Contribution contribution) => CountsInMemory(contribution);
}
