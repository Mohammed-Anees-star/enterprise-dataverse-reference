namespace EnterpriseTicketing.Domain.Exceptions;

/// <summary>
/// Base exception for all domain rule violations.
/// Domain exceptions represent business rule violations that are expected and meaningful.
/// They should produce 400 Bad Request or 422 Unprocessable Entity responses at the API boundary.
///
/// Design note: Domain exceptions carry no stack-trace-sensitive data in their Message.
/// Safe to expose message text to API consumers.
/// </summary>
public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "DOMAIN_RULE_VIOLATION")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message, Exception inner, string errorCode = "DOMAIN_RULE_VIOLATION")
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
