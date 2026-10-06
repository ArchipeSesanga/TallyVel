using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Seeds a realistic volume for query-plan measurement (Assignment 5.3).
/// Never runs on startup. Run manually with:  dotnet run -- --seed-volume
/// Intended for the tallyvel_perf database, not the dev database.
/// </summary>
public static class VolumeSeeder
{
    public const int Stokvels = 5;
    public const int MembersPerStokvel = 50;
    public const int Cycles = 48;

    public static async Task RunAsync(TallyVelDbContext db)
    {
        // Safety: this writes thousands of rows, so refuse to touch anything
        // that isn't clearly a perf database (never the dev database).
        var database = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Database ?? "";
        if (!database.EndsWith("_perf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing to seed volume data into '{database}': only databases whose name ends in '_perf' are allowed.");

        if (await db.Contributions.CountAsync() >= 10_000)
        {
            Console.WriteLine("Volume data already present, skipping.");
            return;
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false; // much faster bulk inserts
        var rng = new Random(42); // fixed seed → same data every run

        for (int s = 1; s <= Stokvels; s++)
        {
            var members = new List<User>();
            for (int m = 1; m <= MembersPerStokvel; m++)
            {
                var user = new User($"perf{s}_{m}@test.local", $"Perf Member {s}-{m}", $"perf-hash-{s}-{m}");
                members.Add(user);
                db.Add(user);
            }

            // The first member creates the stokvel (and becomes its Admin);
            // the rest join through the aggregate, as in SeedData.
            var stokvel = new Stokvel($"Perf Stokvel {s}", 500m, ContributionFrequency.Monthly, members[0].Id);
            foreach (var user in members.Skip(1))
                stokvel.AddMember(user.Id);
            db.Add(stokvel);

            for (int c = 1; c <= Cycles; c++)
            {
                var year = 2023 + (c - 1) / 12;
                var month = ((c - 1) % 12) + 1;
                var label = $"{year}-{month:00}"; // e.g. "2023-01"
                db.Add(new ContributionCycle(stokvel.Id, label));

                var daysInMonth = DateTime.DaysInMonth(year, month);
                foreach (var user in members)
                {
                    var amount = 500m + rng.Next(0, 5) * 100m; // 500–900
                    var contribution = new Contribution(stokvel.Id, user.Id, label, amount);
                    db.Add(contribution);

                    // The constructor stamps "now"; spread the payments across the
                    // cycle's own month instead, so RecordedAt order is realistic.
                    var recordedAt = new DateTimeOffset(year, month, 1 + rng.Next(daysInMonth),
                        rng.Next(0, 24), rng.Next(0, 60), rng.Next(0, 60), TimeSpan.Zero);
                    db.Entry(contribution).Property(x => x.RecordedAt).CurrentValue = recordedAt;
                }
            }

            await db.SaveChangesAsync();   // one save per stokvel keeps batches reasonable
            db.ChangeTracker.Clear();
            Console.WriteLine($"Seeded stokvel {s}/{Stokvels}");
        }

        // Refresh PostgreSQL's statistics so the planner knows the table is now big
        await db.Database.ExecuteSqlRawAsync("ANALYZE \"Contributions\";");
        Console.WriteLine("Done.");
    }
}