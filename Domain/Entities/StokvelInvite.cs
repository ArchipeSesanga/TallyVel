using System.Security.Cryptography;

namespace Domain;

/// <summary>
/// An invitation to join one stokvel. The code is the key: a random, URL-safe string from a
/// cryptographically secure generator, so it cannot be guessed. It is the only thing a joiner needs.
/// </summary>
public sealed class StokvelInvite
{
    public const int DefaultLifetimeDays = 7;

    /// <summary>16 random bytes (128 bits) written as 22 URL-safe characters.</summary>
    public const int CodeBytes = 16;

    public string Code { get; private init; }
    public Guid StokvelId { get; private init; }
    public Guid CreatedByUserId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset ExpiresAt { get; private init; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // For EF Core only.
    private StokvelInvite()
    {
        Code = null!;
    }

    public StokvelInvite(Guid stokvelId, Guid createdByUserId, DateTimeOffset now, TimeSpan? lifetime = null)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("An invite must belong to a stokvel.", nameof(stokvelId));

        Code = NewCode();
        StokvelId = stokvelId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        ExpiresAt = now + (lifetime ?? TimeSpan.FromDays(DefaultLifetimeDays));
    }

    /// <summary>For the demo seed only: an invite that is already past its expiry.</summary>
    public static StokvelInvite Expired(Guid stokvelId, Guid createdByUserId, DateTimeOffset now) =>
        new(stokvelId, createdByUserId, now.AddDays(-14), TimeSpan.FromDays(7));

    /// <summary>True while the invite can still be used: not revoked and not past ExpiresAt.</summary>
    public bool IsUsableAt(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>A fresh code from the operating system's CSPRNG, as URL-safe base64 (no padding).</summary>
    public static string NewCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(CodeBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
