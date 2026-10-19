namespace Domain;

public static class Money
{
    /// <summary>The one place a decimal amount becomes cents.</summary>
    public static long ToCents(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.ToEven);
}