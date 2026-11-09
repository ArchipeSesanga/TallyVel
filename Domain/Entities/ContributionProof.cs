namespace Domain;

/// <summary>
/// The proof of payment (a photo or a PDF) attached to one contribution. At most one per contribution:
/// the contribution's id is the key. The bytes are stored in PostgreSQL, which is fine for files of at most
/// 5 MB in a project this size; a larger system would put them in object storage.
/// </summary>
public sealed class ContributionProof
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxFileNameLength = 100;

    public Guid ContributionId { get; private init; }

    /// <summary>What the file really is, decided by its first bytes. Never what the client claimed.</summary>
    public string ContentType { get; private set; }

    /// <summary>The name to download it as. Already cleaned: no path, no odd characters.</summary>
    public string FileName { get; private set; }

    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public byte[] Content { get; private set; }

    // For EF Core only.
    private ContributionProof()
    {
        ContentType = FileName = Sha256 = null!;
        Content = null!;
    }

    public ContributionProof(
        Guid contributionId, string contentType, string fileName, byte[] content, string sha256,
        Guid uploadedByUserId, DateTimeOffset uploadedAt)
    {
        ContributionId = contributionId;
        ContentType = contentType;
        FileName = fileName;
        Content = content;
        SizeBytes = content.Length;
        Sha256 = sha256;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }

    /// <summary>Replaces the file (allowed while the cycle is open).</summary>
    public void Replace(string contentType, string fileName, byte[] content, string sha256, Guid uploadedByUserId, DateTimeOffset uploadedAt)
    {
        ContentType = contentType;
        FileName = fileName;
        Content = content;
        SizeBytes = content.Length;
        Sha256 = sha256;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }
}
