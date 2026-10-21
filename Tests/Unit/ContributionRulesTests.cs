using Domain;

namespace Tests.Unit;

/// <summary>Which contributions count towards paid, owed, arrears and payout totals (ContributionRules).</summary>
public class ContributionRulesTests
{
    private static Contribution WithStatus(ContributionStatus status) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "2026-10", 100m, status);

    [Theory]
    [InlineData(ContributionStatus.Pending, false)]
    [InlineData(ContributionStatus.Paid, true)]
    [InlineData(ContributionStatus.Confirmed, true)]
    public void Only_Paid_and_Confirmed_contributions_count(ContributionStatus status, bool counts) =>
        Assert.Equal(counts, ContributionRules.IsCounted(WithStatus(status)));

    [Fact]
    public void The_query_form_of_the_rule_agrees_with_the_in_memory_form_for_every_status()
    {
        var query = Enum.GetValues<ContributionStatus>().Select(WithStatus).AsQueryable();

        var counted = query.Where(ContributionRules.Counts).Select(c => c.Status).ToList();

        Assert.Equal(
            Enum.GetValues<ContributionStatus>().Where(s => ContributionRules.IsCounted(WithStatus(s))),
            counted);
    }

    [Fact]
    public void A_contribution_is_Paid_unless_said_otherwise() =>
        Assert.Equal(ContributionStatus.Paid, new Contribution(Guid.NewGuid(), Guid.NewGuid(), "2026-10", 1m).Status);

    [Fact]
    public void An_unknown_status_is_refused() =>
        Assert.Throws<ArgumentException>(() => WithStatus((ContributionStatus)99));
}
