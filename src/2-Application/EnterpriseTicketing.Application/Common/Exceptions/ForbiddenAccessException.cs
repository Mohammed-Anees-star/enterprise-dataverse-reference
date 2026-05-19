namespace EnterpriseTicketing.Application.Common.Exceptions;

/// <summary>Maps to HTTP 403 Forbidden at the API boundary.</summary>
public sealed class ForbiddenAccessException : ApplicationException
{
    public ForbiddenAccessException() : base("Access to this resource is forbidden.") { }
    public ForbiddenAccessException(string message) : base(message) { }
}
