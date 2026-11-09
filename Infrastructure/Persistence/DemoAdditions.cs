using System.Security.Cryptography;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// The demo data that came after the first seed (9.1): stokvel rules, Thandi, Sipho as Treasurer of Work Stokvel,
/// two proofs of payment, and two invites. It runs on EVERY Development start, and each step first checks whether it is
/// already there, so a database that was seeded long ago gains the new demo data and a database that already has it is
/// left alone. It never overwrites anything that exists.
///
/// The demo story it adds:
///  - Thandi (thandi@tallyvel.test) is in no stokvel. She has one VALID invite to Family Stokvel and there is one EXPIRED
///    invite to Work Stokvel. Both codes are written to the log at startup.
///  - Sipho is an ordinary Member of Family Stokvel and the Treasurer of Work Stokvel.
///  - Each of those stokvels has a contribution from ANOTHER member with a proof attached: Thabo's in Family (Sipho is
///    not a treasurer there, so he cannot download it) and Lerato's in Work (Sipho is the treasurer, so he can).
/// </summary>
public static class DemoAdditions
{
    public const string ThandiEmail = "thandi@tallyvel.test";
    public const string SiphoEmail = "sipho@tallyvel.test";

    private const string FamilyRules =
        "Contributions are due by the 5th of every month. A late payment is made up with the next one. " +
        "Decisions about payouts are made together at the monthly meeting.";

    private const string WorkRules =
        "Everyone pays R300 a month on pay day. Payouts rotate in the order agreed at the start of the year. " +
        "Leaving early means waiting until the end of the rotation for any refund.";

    /// <summary>A real 1x1 PNG, so a browser can show it.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");

    /// <summary>A small, valid one-page PDF.</summary>
    private static readonly byte[] TinyPdf = System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
        "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>endobj\n" +
        "trailer<</Root 1 0 R/Size 4>>\n%%EOF\n");

    public static async Task EnsureAsync(
        TallyVelDbContext db, ILogger logger, Func<string> newPasswordHash, TimeProvider clock, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var family = await db.Stokvels.Include(s => s.Members).FirstOrDefaultAsync(s => s.Name == SeedData.FamilyStokvelName, ct);
        var work = await db.Stokvels.Include(s => s.Members).FirstOrDefaultAsync(s => s.Name == SeedData.WorkStokvelName, ct);
        if (family is null || work is null)
            return; // not the demo database: nothing to add to

        var users = await db.Users.Where(u => u.Email.EndsWith("@tallyvel.test")).ToDictionaryAsync(u => u.Email, ct);
        User? Find(string email) => users.GetValueOrDefault(email);

        // 1. Rules, only where none were set.
        if (family.Rules is null) family.SetRules(FamilyRules);
        if (work.Rules is null) work.SetRules(WorkRules);

        // 2. Thandi: in no stokvel, so she can use an invite.
        var thandi = Find(ThandiEmail);
        if (thandi is null)
        {
            thandi = new User(ThandiEmail, "Thandi Ndlovu", newPasswordHash());
            db.Users.Add(thandi);
        }

        // 3. Sipho runs Work Stokvel (and stays an ordinary member of Family).
        var sipho = Find(SiphoEmail);
        if (sipho is not null && work.Members.All(m => m.UserId != sipho.Id))
            work.AddMember(sipho.Id, MemberRole.Treasurer);

        await db.SaveChangesAsync(ct);

        // 4. A proof on one other member's contribution in each of the two stokvels.
        await EnsureProofAsync(db, family, Find("thabo@tallyvel.test"), "thabo-transfer.png", "image/png", TinyPng, now, ct);
        await EnsureProofAsync(db, work, Find(SeedData.LeratoEmail), "lerato-eft.pdf", "application/pdf", TinyPdf, now, ct);

        // 5. One valid invite to Family (made by its Treasurer, Lerato) and one expired invite to Work (by its Chairperson, Nomsa).
        var lerato = Find(SeedData.LeratoEmail);
        var nomsa = Find("nomsa@tallyvel.test");
        string? valid = null, expired = null;
        if (lerato is not null)
        {
            var invite = await db.StokvelInvites.FirstOrDefaultAsync(
                i => i.StokvelId == family.Id && i.CreatedByUserId == lerato.Id && i.RevokedAt == null && i.ExpiresAt > now, ct);
            if (invite is null)
            {
                // A year, so the demo invite does not lapse a week after the database was first seeded.
                invite = new StokvelInvite(family.Id, lerato.Id, now, TimeSpan.FromDays(365));
                db.StokvelInvites.Add(invite);
            }
            valid = invite.Code;
        }
        if (nomsa is not null)
        {
            var invite = await db.StokvelInvites.FirstOrDefaultAsync(
                i => i.StokvelId == work.Id && i.CreatedByUserId == nomsa.Id && i.ExpiresAt < now, ct);
            if (invite is null)
            {
                invite = StokvelInvite.Expired(work.Id, nomsa.Id, now);
                db.StokvelInvites.Add(invite);
            }
            expired = invite.Code;
        }
        await db.SaveChangesAsync(ct);

        // The codes are random secrets, so this log is the only place they are written down for the demo.
        logger.LogInformation(
            "Demo invites for {Thandi} (in no stokvel): VALID code {ValidCode} joins {FamilyStokvel}; EXPIRED code {ExpiredCode} is for {WorkStokvel}",
            ThandiEmail, valid, SeedData.FamilyStokvelName, expired, SeedData.WorkStokvelName);
    }

    /// <summary>Attaches a proof to the member's most recent contribution in the stokvel, unless it already has one.</summary>
    private static async Task EnsureProofAsync(
        TallyVelDbContext db, Stokvel stokvel, User? member, string fileName, string contentType, byte[] content,
        DateTimeOffset now, CancellationToken ct)
    {
        if (member is null) return;

        var contribution = await db.Contributions.AsNoTracking()
            .Where(c => c.StokvelId == stokvel.Id && c.MemberUserId == member.Id)
            .OrderByDescending(c => c.RecordedAt)
            .FirstOrDefaultAsync(ct);
        if (contribution is null) return;
        if (await db.ContributionProofs.AnyAsync(p => p.ContributionId == contribution.Id, ct)) return;

        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        db.ContributionProofs.Add(new ContributionProof(contribution.Id, contentType, fileName, content, sha256, member.Id, now));
        await db.SaveChangesAsync(ct);
    }
}
