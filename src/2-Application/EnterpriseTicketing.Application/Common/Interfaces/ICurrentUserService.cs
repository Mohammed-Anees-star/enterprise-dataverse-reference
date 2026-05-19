namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Provides current user context to application handlers without coupling to ASP.NET Core.
/// Application layer uses this interface; Infrastructure/API provides the implementation.
/// This abstraction makes handlers testable with any user context.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
    bool IsInRole(string role);
}
