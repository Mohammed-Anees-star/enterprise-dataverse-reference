using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseTicketing.API.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal authentication handler for integration tests.
/// Reads X-Test-UserId and X-Test-Roles headers and constructs a
/// <see cref="ClaimsPrincipal"/> that satisfies all [Authorize(Policy=...)] checks.
/// This avoids the need for real Azure AD tokens in CI.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var uid)
                     ? uid.ToString() : "integration-test-user";

        var rolesHeader = Request.Headers.TryGetValue("X-Test-Roles", out var rh)
                          ? rh.ToString() : "Ticket.Reader,Ticket.Writer,Ticket.Manager,Ticket.Admin";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("oid", userId),
            new("name", "Integration Test User"),
            new("preferred_username", "test@example.com")
        };

        foreach (var role in rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries))
            claims.Add(new Claim("roles", role.Trim()));

        var identity  = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
