using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Data;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Puts the <see cref="SeedData"/> sample users and stokvels into an
/// empty database, so a fresh Postgres has something to return — the job
/// the in-memory repositories' constructors used to do. Does nothing if
/// any users or stokvels already exist.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(TallyVelDbContext db)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"The database is missing {pending.Count} migration(s) ({string.Join(", ", pending)}). " +
                "Apply them with: dotnet ef database update --project TallyVel.Api");
        }

        if (await db.Users.AnyAsync() || await db.Stokvels.AnyAsync())
            return;

        var (users, stokvels) = SeedData.Generate();
        db.Users.AddRange(users);
        db.Stokvels.AddRange(stokvels);
        await db.SaveChangesAsync();
    }
}
