using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace EnterpriseTicketing.API.Configuration;

/// <summary>
/// Single canonical source for authentication and authorisation configuration.
///
/// Role constants are defined here and referenced by both the policy builder and
/// the controller <c>[Authorize(Policy = ...)]</c> attributes so that a role name
/// typo is caught at compile time rather than at runtime.
///
/// Role values must match the <c>roles</c> claim emitted by the Azure AD app registration.
/// Declare the roles in the app manifest under "appRoles".
///
/// FIX: The previous implementation had two independent sets of role/policy names:
///   Program.cs used "TicketAgent", "TicketManager", "Administrator" (verbs)
///   AuthenticationConfiguration.cs used "Ticket.Reader", "Ticket.Manager", "Admin" (nouns)
/// This caused runtime 403s for all protected endpoints.
/// Now there is exactly one definition.
/// </summary>
public static class AuthenticationConfiguration
{
    // ─── Role claim values (must match Azure AD app manifest appRoles) ───────
    public const string RoleTicketReader  = "Ticket.Reader";   // read tickets
    public const string RoleTicketWriter  = "Ticket.Writer";   // create/update tickets
    public const string RoleTicketManager = "Ticket.Manager";  // close/escalate/assign
    public const string RoleAdmin         = "Ticket.Admin";    // unrestricted

    // ─── Policy names (used in [Authorize(Policy = PolicyXxx)] attributes) ───
    public const string PolicyTicketRead   = "TicketRead";
    public const string PolicyTicketWrite  = "TicketWrite";
    public const string PolicyTicketManage = "TicketManage";
    public const string PolicyTicketAdmin  = "TicketAdmin";

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Registers JWT Bearer authentication via Microsoft Identity Web.</summary>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        return services;
    }

    /// <summary>Registers role-based authorisation policies.</summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(
                new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build())
            .AddPolicy(PolicyTicketRead, p =>
                p.RequireRole(RoleTicketReader, RoleTicketWriter, RoleTicketManager, RoleAdmin))
            .AddPolicy(PolicyTicketWrite, p =>
                p.RequireRole(RoleTicketWriter, RoleTicketManager, RoleAdmin))
            .AddPolicy(PolicyTicketManage, p =>
                p.RequireRole(RoleTicketManager, RoleAdmin))
            .AddPolicy(PolicyTicketAdmin, p =>
                p.RequireRole(RoleAdmin));

        return services;
    }
}
