using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EnterpriseTicketing.API.Configuration;

/// <summary>
/// Swagger/OpenAPI configuration.
///
/// FIX: The previous implementation called <c>builder.Services.BuildServiceProvider()</c>
/// inside <c>AddSwaggerGen</c> to resolve <see cref="IApiVersionDescriptionProvider"/>.
/// This is an anti-pattern that triggers DI container validation warnings, can cause
/// singleton-scope leaks, and is explicitly flagged by ASP.NET Core's DI diagnostics.
///
/// Correct approach: defer per-version document registration to
/// <see cref="ConfigureSwaggerOptions"/> which implements
/// <see cref="IConfigureOptions{SwaggerGenOptions}"/> and is called by the framework
/// after the full DI container is built, at which point
/// <see cref="IApiVersionDescriptionProvider"/> is safely resolvable.
///
/// Reference: https://github.com/dotnet/aspnet-api-versioning/wiki/Swagger-Integration
/// </summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddApiSwagger(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Register the IConfigureOptions<SwaggerGenOptions> implementation.
        // This is called by SwaggerGen before building the Swagger document,
        // by which time the full DI container (including IApiVersionDescriptionProvider)
        // is available.
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSingleton(configuration);   // expose config for ConfigureSwaggerOptions

        services.AddSwaggerGen(options =>
        {
            // The security scheme is version-independent; register it once here.
            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(
                            $"https://login.microsoftonline.com/{configuration["AzureAd:TenantId"] ?? "common"}/oauth2/v2.0/authorize"),
                        TokenUrl = new Uri(
                            $"https://login.microsoftonline.com/{configuration["AzureAd:TenantId"] ?? "common"}/oauth2/v2.0/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            [$"{configuration["AzureAd:Audience"] ?? "api://enterprise-ticketing"}/access_as_user"]
                                = "Access the API on behalf of the signed-in user"
                        }
                    }
                }
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "oauth2"
                    }
                }] = Array.Empty<string>()
            });

            // Include XML doc comments when the file exists (enabled via
            // <GenerateDocumentationFile>true</GenerateDocumentationFile> in .csproj).
            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");

            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        });

        return services;
    }
}

/// <summary>
/// Registers one Swagger document per discovered API version.
/// Implements <see cref="IConfigureOptions{TOptions}"/> so it is called
/// lazily after the DI container is fully built — no <c>BuildServiceProvider</c> needed.
/// </summary>
internal sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly IConfiguration _configuration;

    public ConfigureSwaggerOptions(
        IApiVersionDescriptionProvider provider,
        IConfiguration configuration)
    {
        _provider = provider;
        _configuration = configuration;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Enterprise Ticketing API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? $"v{description.ApiVersion} is deprecated — please migrate to the latest version."
                    : "Production-grade reference implementation of an enterprise ticket management system.",
                Contact = new OpenApiContact
                {
                    Name = "Platform Engineering",
                    Email = _configuration["Api:SupportEmail"] ?? "platform@example.com"
                }
            });
        }
    }
}
