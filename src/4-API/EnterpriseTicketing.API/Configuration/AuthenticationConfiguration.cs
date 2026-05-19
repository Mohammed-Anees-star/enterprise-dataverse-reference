using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace EnterpriseTicketing.API.Configuration;

/// <summary>
/// Centralises Azure AD / Entra ID JWT Bearer setup and authorization policies.
///
/// The single source of truth for "who is allowed to do what" lives here so that
/// adding a new policy or scope does not require changing controller attributes.
/// </summary>
public static class AuthenticationConfiguration
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));
        return services;
    }

    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Default policy - any authenticated user
            .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            // Role-based policies
            .AddPolicy("TicketReader", p => p.RequireRole("Ticket.Reader", "Ticket.Manager", "Admin"))
            .AddPolicy("TicketWriter", p => p.RequireRole("Ticket.Manager", "Admin"))
            .AddPolicy("TicketAdmin", p => p.RequireRole("Admin"));
        return services;
    }
}
