using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Tests.Integration;

/// <summary>
/// Proves the uniqueness rules hold in PostgreSQL on their own, with no
/// help from the C# checks. Needs the real test database (see PostgresFixture).
/// </summary>
public class DatabaseConstraintTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DatabaseConstraintTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Unique per call, so leftovers from earlier runs can't make a test pass or fail.
    private static string NewLabel() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Saves a user and a stokvel the user created (so they are its first member).</summary>
    private async Task<(User User, Stokvel Stokvel)> SaveStokvelWithMemberAsync()
    {
        var user = new User($"{Guid.NewGuid():N}@test.local", "Constraint Test", "hash");
        var stokvel = new Stokvel($"Constraint Test {Guid.NewGuid():N}", 500m, ContributionFrequency.Monthly, user.Id);

        await using var db = _fixture.CreateContext();
        db.Users.Add(user);
        db.Stokvels.Add(stokvel);
        await db.SaveChangesAsync();

        return (user, stokvel);
    }

    private static PostgresException AssertUniqueViolation(DbUpdateException ex, string constraintName)
    {
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal("23505", pg.SqlState);
        Assert.Equal(constraintName, pg.ConstraintName);
        return pg;
    }

    [Fact]
    public async Task SecondContribution_ForSameMemberAndCycle_IsRejectedByTheDatabase()
    {
        // WHY this bypasses ContributionServices and ExistsForCycle: the C#
        // check is a read followed by a write, so two requests that both
        // pass it at the same moment would both insert. This test goes
        // straight to the database to prove the unique index stops the
        // second one by itself (as it would for a script or a manual fix).
        var (user, stokvel) = await SaveStokvelWithMemberAsync();
        var label = NewLabel();

        await using (var first = _fixture.CreateContext())
        {
            first.ContributionCycles.Add(new ContributionCycle(stokvel.Id, label));
            first.Contributions.Add(new Contribution(stokvel.Id, user.Id, label, 500m));
            await first.SaveChangesAsync();
        }

        // A NEW context: a separate unit of work, like a second request.
        await using (var second = _fixture.CreateContext())
        {
            second.Contributions.Add(new Contribution(stokvel.Id, user.Id, label, 600m));

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
            AssertUniqueViolation(ex, "IX_Contributions_StokvelId_MemberUserId_Cycle");
        }

        // And the database really kept only the first one.
        await using var check = _fixture.CreateContext();
        var stored = await check.Contributions
            .Where(c => c.StokvelId == stokvel.Id && c.MemberUserId == user.Id && c.Cycle == label)
            .ToListAsync();
        var only = Assert.Single(stored);
        Assert.Equal(500m, only.Amount);
    }

    [Fact]
    public async Task SecondCycle_WithSameStokvelAndLabel_IsRejectedByTheDatabase()
    {
        // Bypasses EfContributionRepository.AddAsync (the find-or-create),
        // for the same reason: two requests can both find "no such cycle".
        var (_, stokvel) = await SaveStokvelWithMemberAsync();
        var label = NewLabel();

        await using (var first = _fixture.CreateContext())
        {
            first.ContributionCycles.Add(new ContributionCycle(stokvel.Id, label));
            await first.SaveChangesAsync();
        }

        await using (var second = _fixture.CreateContext())
        {
            second.ContributionCycles.Add(new ContributionCycle(stokvel.Id, label));

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
            AssertUniqueViolation(ex, "AK_ContributionCycles_StokvelId_Label");
        }

        await using var check = _fixture.CreateContext();
        Assert.Equal(1, await check.ContributionCycles.CountAsync(c => c.StokvelId == stokvel.Id && c.Label == label));
    }
}
