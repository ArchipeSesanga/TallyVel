using Domain;

namespace Infrastructure.Persistence;

/// <summary>Everything the seeder inserts, built together so the links between the parts are valid.</summary>
public sealed record SeedSet(
    List<User> Users,
    List<Stokvel> Stokvels,
    List<ContributionCycle> Cycles,
    List<Contribution> Contributions);

/// <summary>
/// Produces the demo data a fresh Development database starts with. All names
/// are made up and every address ends in ".test". Kept as its own class
/// because a Stokvel needs a real User's Id as its creator — seeding them
/// together in one place is what guarantees that link is valid.
///
/// The story it tells (what the frontend's sample data relies on):
///  - Lerato is Treasurer of "Family Stokvel" and an ordinary Member of "Work Stokvel".
///  - In the current cycle she still owes in Family Stokvel (her contribution is
///    Pending, which does not count) and is all paid up in Work Stokvel.
///  - Contributions exist in all three statuses, including exactly 10 cents and
///    one recorded at 2026-10-31T22:30:00Z. Family Stokvel's list holds all of them.
/// </summary>
public static class SeedData
{
    public const string LeratoEmail = "lerato@tallyvel.test";
    public const string FamilyStokvelName = "Family Stokvel";
    public const string WorkStokvelName = "Work Stokvel";

    /// <summary>
    /// The midnight-boundary contribution: 22:30 UTC on 31 October is 00:30 on
    /// 1 November in Johannesburg (UTC+2), so it belongs to the November cycle.
    /// </summary>
    public static readonly DateTimeOffset MidnightBoundaryAt = new(2026, 10, 31, 22, 30, 0, TimeSpan.Zero);

    /// <param name="newPasswordHash">Returns a fresh hash of the demo password; called once per user.</param>
    /// <param name="now">The current instant; the "current cycle" is the South African month it falls in.</param>
    public static SeedSet Generate(Func<string> newPasswordHash, DateTimeOffset now)
    {
        User NewUser(string email, string name) => new(email, name, newPasswordHash());

        var lerato = NewUser(LeratoEmail, "Lerato Mokoena");
        var thabo = NewUser("thabo@tallyvel.test", "Thabo Nkosi");
        var lindiwe = NewUser("lindiwe@tallyvel.test", "Lindiwe Dube");
        var sipho = NewUser("sipho@tallyvel.test", "Sipho Zulu");
        var nomsa = NewUser("nomsa@tallyvel.test", "Nomsa Khumalo");
        var nandi = NewUser("nandi@tallyvel.test", "Nandi Mthembu");
        var users = new List<User> { lerato, thabo, lindiwe, sipho, nomsa, nandi };

        // Family Stokvel: R500 a month. Thabo created it (Chairperson); Lerato is Treasurer.
        var family = new Stokvel(FamilyStokvelName, 500m, ContributionFrequency.Monthly, thabo.Id);
        family.AddMember(lerato.Id, MemberRole.Treasurer);
        family.AddMember(lindiwe.Id);
        family.AddMember(sipho.Id);

        // Work Stokvel: R300 a month. Nomsa created it (Chairperson); Lerato is an ordinary Member.
        var work = new Stokvel(WorkStokvelName, 300m, ContributionFrequency.Monthly, nomsa.Id);
        work.AddMember(lerato.Id);
        work.AddMember(nandi.Id);

        var stokvels = new List<Stokvel> { family, work };

        // Monthly cycles keyed by (stokvel, label), created on first use. A cycle is the
        // South African month a date falls in, so every contribution lands in the cycle
        // that contains the South African date of its RecordedAt.
        var cycles = new Dictionary<(Guid, string), ContributionCycle>();
        ContributionCycle CycleFor(Stokvel stokvel, DateOnly date)
        {
            var startsOn = new DateOnly(date.Year, date.Month, 1);
            var label = StokvelCalendar.LabelFor(startsOn, stokvel.Cycle);
            if (!cycles.TryGetValue((stokvel.Id, label), out var cycle))
            {
                cycle = new ContributionCycle(
                    stokvel.Id, label, startsOn, StokvelCalendar.EndOfCycle(startsOn, stokvel.Cycle));
                cycles[(stokvel.Id, label)] = cycle;
            }
            return cycle;
        }

        var today = StokvelCalendar.Today(now);
        var contributions = new List<Contribution>();

        // Recorded "hoursAgo" before now, but never before the cycle began.
        void Contribute(Stokvel stokvel, User member, decimal amount, ContributionStatus status, int hoursAgo)
        {
            var cycle = CycleFor(stokvel, today);
            var cycleStartsAt = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(cycle.StartsOn!.Value.ToDateTime(TimeOnly.MinValue), StokvelCalendar.Zone),
                TimeSpan.Zero);
            var recordedAt = now.AddHours(-hoursAgo);
            if (recordedAt < cycleStartsAt) recordedAt = cycleStartsAt;
            contributions.Add(new Contribution(stokvel.Id, member.Id, cycle.Label, amount, status, recordedAt));
        }

        // Current cycle, Family Stokvel: Lerato's is Pending, so she still owes R500.
        Contribute(family, thabo, 500m, ContributionStatus.Confirmed, hoursAgo: 72);
        Contribute(family, lindiwe, 500m, ContributionStatus.Paid, hoursAgo: 48);
        Contribute(family, lerato, 500m, ContributionStatus.Pending, hoursAgo: 24);
        Contribute(family, sipho, 0.10m, ContributionStatus.Paid, hoursAgo: 12);   // exactly 10 cents

        // Current cycle, Work Stokvel: Lerato has paid in full.
        Contribute(work, nomsa, 300m, ContributionStatus.Confirmed, hoursAgo: 60);
        Contribute(work, lerato, 300m, ContributionStatus.Paid, hoursAgo: 36);

        // The midnight-boundary row. The domain puts a contribution in the cycle containing the
        // South African date of RecordedAt, which here is 1 November 2026, so it goes in the
        // "2026-11" cycle (1-30 November), not the October one its UTC date suggests.
        // It is in Family Stokvel so that one stokvel's list holds every case the frontend shows.
        var november = CycleFor(family, StokvelCalendar.Today(MidnightBoundaryAt));
        contributions.Add(new Contribution(
            family.Id, sipho.Id, november.Label, 500m, ContributionStatus.Paid, MidnightBoundaryAt));

        return new SeedSet(users, stokvels, cycles.Values.ToList(), contributions);
    }
}
