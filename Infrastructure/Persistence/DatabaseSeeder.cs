using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;

namespace Infrastructure.Persistence;

/// <summary>
/// Puts the <see cref="SeedData"/> demo users, stokvels, cycles and
/// contributions into an empty database, so a fresh Postgres has something to
/// return. Does nothing if any users or stokvels already exist, so running it
/// twice changes nothing. Program.cs only calls it in Development.
/// </summary>
public static class DatabaseSeeder
{
    /// <param name="newPasswordHash">
    /// Returns a hash of the demo password (Seed:DemoPassword), made with the same
    /// password hasher /auth/register uses. Only called when there is something to seed.
    /// </param>
    public static async Task SeedAsync(
        TallyVelDbContext db, ILogger logger, Func<string> newPasswordHash, TimeProvider clock)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"The database is missing {pending.Count} migration(s) ({string.Join(", ", pending)}). " +
                "Apply them with: dotnet ef database update --project Api");
        }

        if (await db.Users.AnyAsync() || await db.Stokvels.AnyAsync())
        {
            logger.LogDebug("Database already has data; skipping seed");
            return;
        }

        var seed = SeedData.Generate(newPasswordHash, clock.GetUtcNow());
        db.Users.AddRange(seed.Users);
        db.Stokvels.AddRange(seed.Stokvels);
        db.ContributionCycles.AddRange(seed.Cycles);
        db.Contributions.AddRange(seed.Contributions);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeded database with {UserCount} users, {StokvelCount} stokvels and {ContributionCount} contributions",
            seed.Users.Count, seed.Stokvels.Count, seed.Contributions.Count);
    }
}
