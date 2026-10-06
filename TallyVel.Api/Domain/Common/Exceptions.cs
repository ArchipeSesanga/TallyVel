namespace TallyVel.Api.Domain;

public abstract class TallyVelException : Exception
{
    public string Code {get;}

    protected TallyVelException(string code, string message) : base(message)
    {
        Code = code;
    }

}

//Specific Faillures

public sealed class PageSizeException : TallyVelException
{
    public const string NegativeRule = "PageSize.Negative";

    public int RequestedSize { get; }

    public PageSizeException(int requestedSize)
        : base(NegativeRule, $"pageSize cannot be negative (received {requestedSize}).")
    {
        RequestedSize = requestedSize;
    }
}

/// <summary>
/// A list query the API refuses: an unknown sort, or a page token that is
/// malformed or was issued for a different filter/sort.
/// </summary>
public sealed class InvalidQueryException : TallyVelException
{
    public string Parameter { get; }

    public InvalidQueryException(string parameter, string message)
        : base($"invalid-{parameter.ToLowerInvariant()}", message)
    {
        Parameter = parameter;
    }
}

public sealed class NotFoundException : TallyVelException
{
    public NotFoundException(string resource, object key, string? message = null)
    : base ($"{resource}-not-found", message ?? $"No  {resource} found with id {key}")
    {
        
    }
    
}

public sealed class BusinessRuleViolationException: TallyVelException
{
    public BusinessRuleViolationException(string rule, string message)
    : base(rule, message)
    {
        
    }
}

public sealed class AlreadyExistsException: TallyVelException
{
    public AlreadyExistsException(string code, string message)
    : base( code ,message )
    {
        
    }
}

public sealed class IdempotencyKeyReusedException : TallyVelException
{
    public string IdempotencyKey { get; }
 
    public IdempotencyKeyReusedException(string idempotencyKey)
        : base("idempotency-key-reused",
               $"Idempotency-Key '{idempotencyKey}' was already used for a different request. Use a new key for a new operation.")
    {
        IdempotencyKey = idempotencyKey;
    }
}

public sealed class IdempotencyKeyInProgressException : TallyVelException
{
    public string IdempotencyKey { get; }
 
    public IdempotencyKeyInProgressException(string idempotencyKey)
        : base("idempotency-key-in-progress",
               $"A request with Idempotency-Key '{idempotencyKey}' is still being processed. Retry shortly.")
    {
        IdempotencyKey = idempotencyKey;
    }
}