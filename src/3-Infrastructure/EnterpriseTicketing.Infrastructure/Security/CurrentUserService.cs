using EnterpriseTicketing.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EnterpriseTicketing.Infrastructure.Security;

/// <summary>
/// Extracts current user context from the ASP.NET Core HTTP context.
/// Implements the Application layer's ICurrentUserService abstraction.
///
/// The Application layer uses ICurrentUserService; it has no knowledge of HttpContext,
/// ClaimsPrincipal, or JWT structure. This separation enables testing handlers
/// with any user context by substituting a test implementation.
///
/// Claim type mapping is Azure AD / Entra ID specific.
/// Adjust claim types for other identity providers.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("oid")     // Azure AD object ID
        ?? Principal?.FindFirstValue("sub");

    public string? UserName => Principal?.FindFirstValue("name")
        ?? Principal?.FindFirstValue(ClaimTypes.Name);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("preferred_username");

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IEnumerable<string> Roles =>
        Principal?.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
            .Select(c => c.Value)
        ?? [];

    public bool IsInRole(string role) =>
        Principal?.IsInRole(role) == true;
}
