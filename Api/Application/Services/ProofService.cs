using System.Security.Cryptography;
using System.Text;
using Api.Application.Contracts;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>
/// Proof of payment: who may attach, replace, remove and download it, and what a file must look like.
/// Every rule is read from the database. The one access rule (<see cref="CanAccess"/>) is used by upload,
/// delete, download AND the canDownloadProof flag, so they can never disagree.
/// </summary>
public sealed class ProofService(TallyVelDbContext db, TimeProvider clock, ILogger<ProofService> logger)
{
    /// <summary>The contribution's own member, or a Treasurer or Chairperson of THIS stokvel.</summary>
    public static bool CanAccess(Guid contributionMemberId, Guid callerId, MemberRole? callerRoleInThisStokvel) =>
        callerId == contributionMemberId || callerRoleInThisStokvel is MemberRole.Treasurer or MemberRole.Chairperson;

    private async Task<MemberRole?> RoleAsync(Guid stokvelId, Guid userId, CancellationToken ct) =>
        await db.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId && m.UserId == userId)
            .Select(m => (MemberRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    private async Task<Contribution?> FindContributionAsync(Guid stokvelId, Guid contributionId, CancellationToken ct) =>
        await db.Contributions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contributionId && c.StokvelId == stokvelId, ct);

    // ---- upload / replace ----

    /// <returns>The stored proof, and whether it replaced an earlier one.</returns>
    public async Task<(ProofResponse Proof, bool Replaced)> SaveAsync(
        Guid stokvelId, Guid contributionId, Guid callerId, string? clientFileName, byte[] content, CancellationToken ct)
    {
        var contribution = await FindContributionAsync(stokvelId, contributionId, ct)
            ?? throw new NotFoundException("contribution", contributionId);

        var role = await RoleAsync(stokvelId, callerId, ct);
        if (!CanAccess(contribution.MemberUserId, callerId, role))
            throw new ForbiddenActionException("proof-forbidden",
                "Only the member who paid, or this stokvel's Treasurer, can attach proof of payment.");

        await EnsureCycleOpenAsync(contribution, ct);

        if (content.Length == 0)
            throw new ArgumentException("The file is empty.");
        if (content.Length > ContributionProof.MaxBytes)
            throw new PayloadTooLargeException("file-too-large",
                $"The file is larger than {ContributionProof.MaxBytes / (1024 * 1024)} MB.");

        // The type comes from the file's own first bytes. The name and the client's Content-Type are not trusted.
        var type = ProofFileType.Detect(content)
            ?? throw new UnsupportedFileTypeException("unsupported-file-type",
                "Only JPEG, PNG and PDF files are accepted.");

        var fileName = ProofFileType.SafeFileName(clientFileName, type.Extension);
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var now = clock.GetUtcNow();

        var existing = await db.ContributionProofs.FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct);
        if (existing is null)
            db.ContributionProofs.Add(new ContributionProof(contributionId, type.ContentType, fileName, content, sha256, callerId, now));
        else
            existing.Replace(type.ContentType, fileName, content, sha256, callerId, now);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Proof {Action} for contribution {ContributionId} ({ContentType}, {Size} bytes)",
            existing is null ? "attached" : "replaced", contributionId, type.ContentType, content.Length);

        return (new ProofResponse(contributionId, type.ContentType, fileName, content.Length, sha256, now), existing is not null);
    }

    // ---- delete ----

    public async Task DeleteAsync(Guid stokvelId, Guid contributionId, Guid callerId, CancellationToken ct)
    {
        var contribution = await FindContributionAsync(stokvelId, contributionId, ct)
            ?? throw new NotFoundException("contribution", contributionId);

        var role = await RoleAsync(stokvelId, callerId, ct);
        if (!CanAccess(contribution.MemberUserId, callerId, role))
            throw new ForbiddenActionException("proof-forbidden",
                "Only the member who paid, or this stokvel's Treasurer, can remove proof of payment.");

        await EnsureCycleOpenAsync(contribution, ct);

        var proof = await db.ContributionProofs.FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct)
            ?? throw new NotFoundException("proof", contributionId, "There is no proof of payment on this contribution.");

        db.ContributionProofs.Remove(proof);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Proof removed from contribution {ContributionId}", contributionId);
    }

    // ---- download ----

    /// <summary>
    /// The file, or a 404 that is the SAME for every reason: no such contribution, no proof on it, or the
    /// caller is not allowed to see it. Someone who may not download it cannot tell whether a proof exists.
    /// </summary>
    public async Task<ContributionProof> GetForDownloadAsync(
        Guid stokvelId, Guid contributionId, Guid callerId, CancellationToken ct)
    {
        var notFound = new NotFoundException("proof", contributionId, "There is no proof of payment to download here.");

        var contribution = await FindContributionAsync(stokvelId, contributionId, ct) ?? throw notFound;
        var role = await RoleAsync(stokvelId, callerId, ct);
        if (!CanAccess(contribution.MemberUserId, callerId, role)) throw notFound;

        return await db.ContributionProofs.AsNoTracking().FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct)
            ?? throw notFound;
    }

    // ---- flags for the contribution lists ----

    /// <summary>For each contribution: does it have a proof, and may the caller download it (by the same rule as the download).</summary>
    public async Task<IReadOnlyDictionary<Guid, (bool HasProof, bool CanDownload)>> FlagsAsync(
        Guid stokvelId, Guid callerId, IReadOnlyCollection<Contribution> contributions, CancellationToken ct)
    {
        if (contributions.Count == 0) return new Dictionary<Guid, (bool, bool)>();

        var ids = contributions.Select(c => c.Id).ToList();
        var withProof = (await db.ContributionProofs.AsNoTracking()
                .Where(p => ids.Contains(p.ContributionId)).Select(p => p.ContributionId).ToListAsync(ct))
            .ToHashSet();
        var role = await RoleAsync(stokvelId, callerId, ct);

        return contributions.ToDictionary(
            c => c.Id,
            c => (withProof.Contains(c.Id), withProof.Contains(c.Id) && CanAccess(c.MemberUserId, callerId, role)));
    }

    // ---- shared ----

    /// <summary>Once the contribution's cycle is closed or paid out, its proof is part of the record and cannot change.</summary>
    private async Task EnsureCycleOpenAsync(Contribution contribution, CancellationToken ct)
    {
        var status = await db.ContributionCycles.AsNoTracking()
            .Where(c => c.StokvelId == contribution.StokvelId && c.Label == contribution.Cycle)
            .Select(c => c.Status)
            .FirstAsync(ct);

        if (status != CycleStatus.Open)
            throw new AlreadyExistsException("proof-locked",
                "This contribution's cycle is closed, so its proof of payment can no longer be changed.");
    }
}

/// <summary>What a proof file may be, and how its name is cleaned.</summary>
public static class ProofFileType
{
    public sealed record Detected(string ContentType, string Extension);

    /// <summary>JPEG, PNG or PDF by the file's first bytes (its "magic bytes"), or null for anything else.</summary>
    public static Detected? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return new Detected("image/jpeg", ".jpg");

        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.StartsWith(png))
            return new Detected("image/png", ".png");

        ReadOnlySpan<byte> pdf = "%PDF-"u8;
        if (bytes.StartsWith(pdf))
            return new Detected("application/pdf", ".pdf");

        return null;
    }

    /// <summary>
    /// A file name that is safe to send back in a header: no directories, only letters, digits, space, dot, dash and
    /// underscore, at most 100 characters, and the extension of the DETECTED type (not whatever the client wrote).
    /// </summary>
    public static string SafeFileName(string? clientName, string extension)
    {
        var name = clientName ?? "";
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..]; // drop any directory part, with either slash
        var dot = name.LastIndexOf('.');
        var stem = dot > 0 ? name[..dot] : name;

        var cleaned = new StringBuilder();
        foreach (var ch in stem)
            cleaned.Append(char.IsAsciiLetterOrDigit(ch) || ch is ' ' or '-' or '_' or '.' ? ch : '_');

        var safe = cleaned.ToString().Trim(' ', '.');
        if (safe.Length == 0) safe = "proof";
        if (safe.Length > ContributionProof.MaxFileNameLength - extension.Length)
            safe = safe[..(ContributionProof.MaxFileNameLength - extension.Length)];
        return safe + extension;
    }
}
