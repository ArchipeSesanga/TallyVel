namespace TallyVel.Api.Domain;

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
    string? PageToken)
{
     public string Fingerprint() =>
        $"{StokvelId}|{Cycle}|{MemberUserId}|{MinAmount}|{MaxAmount}|{SortBy}|{Descending}";
     
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