namespace Domain;

/// <summary>
/// One refresh token. Only the SHA-256 hash is stored, never the raw token.
/// Tokens form a chain: each exchange marks the old one used and issues a
/// new one with the same <see cref="FamilyId"/>, so a reused token can
/// revoke everything descended from that login.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string TokenHash { get; set; } = default!;  // SHA-256, never the raw token
    public Guid FamilyId { get; set; }                  // every token from one login shares this

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }         // set when it's exchanged
    public Guid? ReplacedByTokenId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
