using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// One stokvel with 40 members and a year of monthly contributions (9.2), so that a performance measurement such as
/// Lighthouse sees a page of realistic size, not the three-person demo. Development and test databases only: it runs
/// from <see cref="DatabaseSeeder"/>, which Program.cs only calls in Development.
///
/// It is made of invented people (made-up names, ".test" email addresses at burial.test) and touches none of the
/// existing demo users, so the demo accounts' own stokvels and totals are unchanged. Sign in as
/// member01@burial.test (its Chairperson) with the demo password to see it. Like DemoAdditions it checks first and adds
/// nothing if the stokvel is already there, so it can run on every start.
///
/// The year: 12 monthly cycles ending with the current one. The 11 earlier cycles are closed and every member paid in
/// each; in the current cycle 28 of the 40 have paid, so 12 still owe (a close preview has something to show).
/// </summary>
public static class PerformanceDemo
{
    public const string StokvelName = "Mabuza Burial Society";
    public const string ChairEmail = "member01@burial.test";
    public const int MemberCount = 40;
    public const int Months = 12;
    public const int PaidInCurrentCycle = 28;
    public const decimal Amount = 300m;

    private static readonly string[] FirstNames =
    [
        "Ayanda", "Bongani", "Cebo", "Dineo", "Enock", "Fikile", "Gugu", "Hlengiwe", "Itumeleng", "Jabu",
        "Kagiso", "Lwazi", "Mpho", "Nokuthula", "Olwethu", "Palesa", "Quinton", "Refilwe", "Sbusiso", "Thuli",
    ];

    private static readonly string[] LastNames =
    [
        "Mabuza", "Ngcobo", "Khumalo", "Sithole", "Molefe", "Dube", "Zwane", "Mahlangu", "Nkosi", "Radebe",
        "Tshabalala", "Masilela", "Cele", "Moloi", "Ndaba", "Shange", "Buthelezi", "Maseko", "Mkhize", "Pillay",
    ];

    public static async Task EnsureAsync(
        TallyVelDbContext db, ILogger logger, Func<string> newPasswordHash, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Stokvels.AnyAsync(s => s.Name == StokvelName, ct))
            return;

        var now = clock.GetUtcNow();
        var today = StokvelCalendar.Today(now);
        var hash = newPasswordHash(); // one hash for all 40: hashing is slow on purpose, and they are all demo people

        // 1. Forty made-up people. The first is the Chairperson, the second the Treasurer.
        var members = new List<User>();
        for (var i = 0; i < MemberCount; i++)
        {
            var name = $"{FirstNames[i % FirstNames.Length]} {LastNames[(i * 7 + i / FirstNames.Length) % LastNames.Length]}";
            members.Add(new User($"member{i + 1:00}@burial.test", name, hash));
        }
        db.Users.AddRange(members);

        var stokvel = new Stokvel(StokvelName, Amount, ContributionFrequency.Monthly, members[0].Id,
            "Everyone pays R300 on the first Saturday of the month. A payout goes to one member each month, in turn.");
        foreach (var (member, index) in members.Select((m, i) => (m, i)).Skip(1))
            stokvel.AddMember(member.Id, index == 1 ? MemberRole.Treasurer : MemberRole.Member);
        db.Stokvels.Add(stokvel);

        // 2. A year of monthly cycles, ending with the current one.
        var currentStart = new DateOnly(today.Year, today.Month, 1);
        var cycles = new List<ContributionCycle>();
        for (var back = Months - 1; back >= 0; back--)
        {
            var startsOn = currentStart.AddMonths(-back);
            cycles.Add(new ContributionCycle(
                stokvel.Id, StokvelCalendar.LabelFor(startsOn, ContributionFrequency.Monthly), startsOn,
                StokvelCalendar.EndOfCycle(startsOn, ContributionFrequency.Monthly)));
        }
        db.ContributionCycles.AddRange(cycles);

        // 3. Contributions: everyone in the closed months, 28 of 40 in the current one.
        foreach (var cycle in cycles)
        {
            var isCurrent = cycle.StartsOn == currentStart;
            foreach (var (member, index) in members.Select((m, i) => (m, i)))
            {
                if (isCurrent && index >= PaidInCurrentCycle) continue;

                // Spread across the first days of the month (07:00 UTC is 09:00 in South Africa), never in the future.
                var recordedAt = new DateTimeOffset(cycle.StartsOn!.Value.Year, cycle.StartsOn.Value.Month, 1 + index % 5, 7, index % 60, 0, TimeSpan.Zero);
                if (recordedAt > now) recordedAt = now.AddMinutes(-(MemberCount - index));
                var status = isCurrent ? ContributionStatus.Paid : ContributionStatus.Confirmed;
                db.Contributions.Add(new Contribution(stokvel.Id, member.Id, cycle.Label, Amount, status, recordedAt));
            }
        }
        await db.SaveChangesAsync(ct);

        // 4. The earlier cycles are closed. ClosedAt is only ever set by a conditional update (the database decides),
        //    never by C# code, so the seed does the same.
        foreach (var past in cycles.Where(c => c.StartsOn < currentStart))
        {
            var closesAt = StokvelCalendar.ClosesAt(past.EndsOn!.Value);
            await db.ContributionCycles.Where(c => c.Id == past.Id && c.ClosedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAt, closesAt), ct);
        }

        logger.LogInformation(
            "Performance demo: {Stokvel} has {Members} members and {Cycles} monthly cycles; sign in as {Email} with the demo password",
            StokvelName, MemberCount, Months, ChairEmail);
    }
}
