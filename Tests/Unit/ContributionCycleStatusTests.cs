using Domain;

namespace Tests.Unit;

/// <summary>Open, Closed or PaidOut: one rule (ContributionCycle.StatusOf), used everywhere a status is returned.</summary>
public class ContributionCycleStatusTests
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 10, 31, 22, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false, CycleStatus.Open)]
    [InlineData(false, true, CycleStatus.Closed)]
    [InlineData(true, false, CycleStatus.PaidOut)]
    [InlineData(true, true, CycleStatus.PaidOut)]   // paid out wins over closed
    public void Status_is_PaidOut_else_Closed_else_Open(bool paidOut, bool closed, CycleStatus expected) =>
        Assert.Equal(expected, ContributionCycle.StatusOf(paidOut, closed ? ClosedAt : null));

    [Fact]
    public void A_new_cycle_is_Open_and_paying_it_out_makes_it_PaidOut()
    {
        var cycle = new ContributionCycle(Guid.NewGuid(), "2026-10");
        Assert.Equal(CycleStatus.Open, cycle.Status);

        cycle.MarkPaidOut();

        Assert.Equal(CycleStatus.PaidOut, cycle.Status);
        Assert.Throws<InvalidOperationException>(cycle.MarkPaidOut);
    }
}
