namespace Domain;

public enum ContributionSortField { RecordedAt, Amount }
public sealed record contributionQuery(
    Guid StokvelId,
    string? Cycle,                 // null = whole stokvel
    Guid? MemberUserId,
    decimal? MinAmount,
    decimal? MaxAmount,
    ContributionSortField SortBy,
    bool Descending,
    int PageSize,
    string? PageToken,
    DateTimeOffset? From = null,   // UTC instant, INCLUDED: recordedAt >= From
    DateTimeOffset? To = null)     // UTC instant, EXCLUDED: recordedAt < To
{
     // Everything that changes WHICH rows match or in WHAT order. A page token carries this, so a token
     // made under one filter is refused under another (pageSize is not in it: it only sets the page length).
     public string Fingerprint() =>
        $"{StokvelId}|{Cycle}|{MemberUserId}|{MinAmount}|{MaxAmount}|{SortBy}|{Descending}|{Instant(From)}|{Instant(To)}";

     private static string Instant(DateTimeOffset? value) =>
        value is { } v ? v.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) : "";
     
     //this method returns a switch value
     public static (ContributionSortField Field, bool Desc) ParseSort(string? sort) => sort switch
    {

        //conditions and their return values 
        null or "recordedAt" => (ContributionSortField.RecordedAt, false),
        "-recordedAt"        => (ContributionSortField.RecordedAt, true),
        "amount"             => (ContributionSortField.Amount, false),
        "-amount"            => (ContributionSortField.Amount, true),
        _ => throw new InvalidQueryException("sort",
                 $"Unknown sort '{sort}'. Allowed: recordedAt, -recordedAt, amount, -amount.")
    };
    
}