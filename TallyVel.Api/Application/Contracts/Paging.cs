using TallyVel.Api.Domain;

namespace TallyVel.Api.Application.Contracts;

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int ResolvePageSize(int? requested )
    {
        if (requested < 0)
           throw new PageSizeException(requested.Value);
           
        if (requested is null or 0) return DefaultPageSize;
        return Math.Min(requested.Value, MaxPageSize);
        
    }
}