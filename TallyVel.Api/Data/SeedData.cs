using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

/// <summary>
/// Produces a small, consistent set of sample Users and Stokvels so the
/// API has something to return before real persistence exists. Kept as
/// its own class, rather than seeded inline in each repository,
/// because a Stokvel needs a real User's Id as its creator — seeding
/// them together in one place is what guarantees that link is valid.
/// </summary>
public static class SeedData
{
    public static (List<User> Users, List<Stokvel> Stokvels) Generate()
    {

        //My default users
        var thabo = new User("thabo@example.com", "Thabo Nkosi", "seed-hash-1");
        var lindiwe = new User("lindiwe@example.com", "Lindiwe Dube", "seed-hash-2");
        var sipho = new User("sipho@example.com", "Sipho Zulu", "seed-hash-3");
        var amahle = new User("amahle@example.com", "Amahle Mokoena", "seed-hash-4");
       
        var users = new List<User> { thabo, lindiwe, sipho, amahle };

        var familySavings = new Stokvel("Family Savings Circle", 500m, ContributionCycle.Monthly, thabo.Id);
        familySavings.AddMember(lindiwe.Id);
        familySavings.AddMember(sipho.Id);

        var weeklyPool = new Stokvel("Weekly Grocery Pool", 150m, ContributionCycle.Weekly, lindiwe.Id);
        weeklyPool.AddMember(amahle.Id, MemberRole.Admin);

        var stokvels = new List<Stokvel> { familySavings, weeklyPool };

        return (users, stokvels);
    }
}