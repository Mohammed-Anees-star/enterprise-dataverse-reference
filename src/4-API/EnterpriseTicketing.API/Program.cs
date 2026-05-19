using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using EnterpriseTicketing.API.Middleware;
using EnterpriseTicketing.Application;
using EnterpriseTicketing.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

// ============================================================
// SERILOG BOOTSTRAP LOGGER
// Captures any startup errors before full DI/config is ready.
// This is critical — without it, startup failures are silent.
// ============================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Enterprise Ticketing API");

    var builder = WebApplication.CreateBuilder(args);

    // ============================================================
    // SERILOG — Full structured logging from configuration
    // ReadFrom.Configuration reads from appsettings.json Serilog section.
    // Supports multiple sinks: Console, Application Insights, Seq, etc.
    // ============================================================
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithCorrelationId();
    });

    // ============================================================
    // APPLICATION INSIGHTS
    // ============================================================
    builder.Services.AddApplicationInsightsTelemetry();

    // ============================================================
    // AUTHENTICATION — Azure AD / Entra ID JWT Bearer
    // Microsoft.Identity.Web handles token validation, caching, and
    // all the complexity of Azure AD token validation.
    // ============================================================
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}";
            options.Audience = builder.Configuration["AzureAd:Audience"];
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(ctx.Exception, "JWT authentication failed");
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("TicketRead", policy => policy.RequireAuthenticatedUser());
        options.AddPolicy("TicketWrite", policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole("TicketAgent", "TicketManager", "Administrator"));
        options.AddPolicy("TicketAdmin", policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole("TicketManager", "Administrator"));
    });

    // ============================================================
    // API VERSIONING
    // URL path versioning is the most visible and explicit strategy.
    // Query string and header versioning work but are less discoverable.
    // ============================================================
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true; // Adds api-supported-versions header to responses
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // ============================================================
    // SWAGGER / OPENAPI
    // ============================================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var provider = builder.Services.BuildServiceProvider()
            .GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Enterprise Ticketing API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "This API version is deprecated. Migrate to the latest version."
                    : "Enterprise support ticket management system.",
                Contact = new OpenApiContact { Name = "Platform Engineering", Email = "platform@company.com" }
            });
        }

        // OAuth2 security definition for Swagger UI testing
        options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri($"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        [$"{builder.Configuration["AzureAd:Audience"]}/.default"] = "Access Enterprise Ticketing API"
                    }
                }
            }
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                },
                []
            }
        });

        // Include XML documentation comments in Swagger
        var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);
    });

    // ============================================================
    // CONTROLLERS
    // ============================================================
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // ============================================================
    // HEALTH CHECKS
    // Enterprise health check strategy:
    //   /health       → full dependency check (Dataverse, Service Bus) — for load balancer
    //   /health/ready → readiness probe — k8s/Container Apps ready to serve traffic
    //   /health/live  → liveness probe — process is alive, restart if failing
    // ============================================================
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddAzureServiceBusQueue(
            builder.Configuration["ServiceBus:ConnectionString"] ?? "placeholder",
            builder.Configuration["ServiceBus:TicketEventsQueueName"] ?? "ticket-events",
            name: "servicebus",
            failureStatus: HealthStatus.Degraded,
            tags: ["ready"]);

    // ============================================================
    // MIDDLEWARE REGISTRATIONS (as transient services)
    // ============================================================
    builder.Services.AddTransient<CorrelationIdMiddleware>();
    builder.Services.AddTransient<ExceptionHandlingMiddleware>();
    builder.Services.AddTransient<SecurityHeadersMiddleware>();

    // ============================================================
    // APPLICATION + INFRASTRUCTURE DI
    // ============================================================
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ============================================================
    // BUILD APPLICATION
    // ============================================================
    var app = builder.Build();

    // ============================================================
    // MIDDLEWARE PIPELINE
    // ORDER MATTERS. Incorrect ordering causes subtle production bugs.
    //
    // 1. CorrelationId — must be first to thread correlation through all subsequent middleware
    // 2. SecurityHeaders — applied to all responses
    // 3. ExceptionHandling — catches exceptions from all downstream middleware
    // 4. SerilogRequestLogging — logs after exception handling (accurate status codes)
    // 5. Swagger — development tool
    // 6. HTTPS Redirection
    // 7. Authentication — must be before Authorization
    // 8. Authorization
    // 9. Controllers
    // 10. Health Checks — endpoints, not middleware
    // ============================================================
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000}ms | CorrelationId: {CorrelationId}";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("oid")?.Value ?? "anonymous");
            if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
                diagnosticContext.Set("CorrelationId", correlationId);
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"Enterprise Ticketing API {description.ApiVersion}");
            }

            options.OAuthClientId(app.Configuration["AzureAd:ClientId"]);
            options.OAuthUsePkce();
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live")
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Enterprise Ticketing API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
