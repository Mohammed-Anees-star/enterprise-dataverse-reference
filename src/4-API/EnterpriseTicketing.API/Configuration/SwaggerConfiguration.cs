using Microsoft.OpenApi.Models;

namespace EnterpriseTicketing.API.Configuration;

/// <summary>
/// Swagger/OpenAPI configuration. Generates per-version document groups
/// (v1, v2) and wires the OAuth2 security scheme so the Try-It-Out flow
/// can acquire a real token in development.
/// </summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Enterprise Ticketing API",
                Version = "v1",
                Description = "Production-grade reference implementation of an enterprise ticket management system over Microsoft Dataverse.",
                Contact = new OpenApiContact { Name = "Platform Engineering", Email = "platform@example.com" }
            });
            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Enterprise Ticketing API",
                Version = "v2",
                Description = "v2 introduces a richer ticket schema and supports OData-style filtering."
            });

            var tenantId = configuration["AzureAd:TenantId"] ?? "common";
            var authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize";
            var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            var audience = configuration["AzureAd:Audience"] ?? "api://enterprise-ticketing";

            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(authUrl),
                        TokenUrl = new Uri(tokenUrl),
                        Scopes = new Dictionary<string, string>
                        {
                            [$"{audience}/access_as_user"] = "Access the API as the signed-in user"
                        }
                    }
                }
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                }] = new[] { $"{audience}/access_as_user" }
            });

            // Include XML comments where available
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });
        return services;
    }
}
