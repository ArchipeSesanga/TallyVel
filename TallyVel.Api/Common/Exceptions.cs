namespace TallyVel.Api.Common;

/// <summary>Requested resource does not exist. Maps to 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Request conflicts with existing state (e.g. duplicate). Maps to 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Well-formed request rejected by a domain rule. Maps to 422.</summary>
public class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message) : base(message) { }
}
