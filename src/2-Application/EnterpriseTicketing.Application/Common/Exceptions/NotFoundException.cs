namespace EnterpriseTicketing.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist.
/// Maps to HTTP 404 at the API boundary.
/// Separate from domain TicketNotFoundException so the application layer
/// can also raise not-found for cross-aggregate lookups.
/// </summary>
public sealed class NotFoundException : ApplicationException
{
    public NotFoundException(string name, object key)
        : base($"Entity '{name}' with key '{key}' was not found.")
    {
    }
}
