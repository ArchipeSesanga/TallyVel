using System.Text.RegularExpressions;

namespace TallyVel.Api.Domain;

/// <summary>
/// A registered TallyVel user. A User is deliberately "dumb" about
/// stokvels — it knows nothing about which stokvels it belongs to or
/// what role it holds in each, because that's per-relationship data
/// owned by Stokvel's membership list, not a property of the person.
///
/// The only way to obtain a User is through the constructor, which
/// validates everything up front — there is no path that produces a
/// User with a malformed email, a blank name, or a raw (unhashed)
/// password.
/// </summary>
public sealed class User
{
    public const int MaxFullNameLength = 150;

    public Guid Id { get; }
    public string Email { get; private set; }
    public string FullName { get; private set; }

    // Stored as a hash, never a raw password — there is no property or
    // constructor path that lets a plaintext password become part of a
    // User.
    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public User(string email, string fullName, string passwordHash)
    {
        Email = ValidateEmail(email);
        FullName = ValidateFullName(fullName);
        PasswordHash = ValidatePasswordHash(passwordHash);

        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Re-validates the same way the constructor does — there is no
    /// back door that lets Email become malformed after the fact.
    /// </summary>
    public void ChangeEmail(string newEmail) => Email = ValidateEmail(newEmail);

    public void ChangeFullName(string newFullName) => FullName = ValidateFullName(newFullName);

    private static string ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var trimmed = email.Trim();

        if (!Regex.IsMatch(trimmed, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Email is not a valid address.", nameof(email));

        return trimmed.ToLowerInvariant();
    }

    private static string ValidateFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        var trimmed = fullName.Trim();

        if (trimmed.Length > MaxFullNameLength)
            throw new ArgumentException($"Full name cannot exceed {MaxFullNameLength} characters.", nameof(fullName));

        return trimmed;
    }

    private static string ValidatePasswordHash(string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A password hash is required — User never accepts a raw password.", nameof(passwordHash));

        return passwordHash;
    }
}