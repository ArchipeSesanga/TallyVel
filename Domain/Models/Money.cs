namespace Domain;

public static class Money
{
    /// <summary>
    /// The largest amount, in cents, that JavaScript can hold exactly (2^53 - 1).
    /// Requests above it are refused, so a client never reads back a rounded number.
    /// </summary>
    public const long MaxCents = 9_007_199_254_740_991;

    /// <summary>The one place a decimal amount becomes cents.</summary>
    public static long ToCents(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.ToEven);

    /// <summary>The one place cents become a decimal amount. Decimal arithmetic only, never double.</summary>
    public static decimal FromCents(long cents) => cents / 100m;
}
