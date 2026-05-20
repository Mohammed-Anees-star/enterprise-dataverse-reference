using Asp.Versioning.ApiExplorer;
using EnterpriseTicketing.API.Configuration;
using EnterpriseTicketing.API.Middleware;
using EnterpriseTicketing.Application;
using EnterpriseTicketing.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

// ─────────────────────────────────────────────────────────────────────────────
// BOOTSTRAP LOGGER
// Captures fatal startup errors before the full DI container is ready.
// ─────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Enterprise Ticketing API");

    var builder = WebApplication.CreateBuilder(args);

    // ─────────────────────────────────────────────────────────────────────────
    // SERILOG — reads full config from appsettings.json "Serilog" section.
    // Enrichers run after DI is ready (ReadFrom.Services) so that
    // IHttpContextAccessor-backed enrichers work correctly.
    // ─────────────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((context, services, cfg) =>
        cfg.ReadFrom.Configuration(context.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithMachineName()
           .Enrich.WithThreadId()
           .Enrich.WithCorrelationId());

    // ─────────────────────────────────────────────────────────────────────────
    // APPLICATION INSIGHTS
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddApplicationInsightsTelemetry();

    // ─────────────────────────────────────────────────────────────────────────
    // AUTHENTICATION & AUTHORISATION
    // Single canonical definition — AuthenticationConfiguration.cs owns all
    // policy names.  Controllers reference these same constants.
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddApiAuthentication(builder.Configuration);
    builder.Services.AddApiAuthorization();

    // ─────────────────────────────────────────────────────────────────────────
    // API VERSIONING
    // URL-path versioning is the most visible strategy for enterprise APIs.
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services
        .AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ReportApiVersions = true;         // adds api-supported-versions response header
        })
        .AddApiExplorer(o =>
        {
            o.GroupNameFormat = "'v'VVV";
            o.SubstituteApiVersionInUrl = true;
        });

    // ─────────────────────────────────────────────────────────────────────────
    // SWAGGER / OPENAPI
    // FIX: No BuildServiceProvider() call — Swagger docs are registered via
    // IPostConfigureOptions<SwaggerGenOptions> in SwaggerConfiguration.cs
    // so that IApiVersionDescriptionProvider is resolved at the correct time.
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddApiSwagger(builder.Configuration);

    // ─────────────────────────────────────────────────────────────────────────
    // CONTROLLERS
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // ─────────────────────────────────────────────────────────────────────────
    // HEALTH CHECKS
    //   /health/live  — liveness probe (process alive)
    //   /health/ready — readiness probe (dependencies reachable)
    //   /health       — full check (for monitoring dashboards)
    // ─────────────────────────────────────────────────────────────────────────
    var sbConnection = builder.Configuration["ServiceBus:ConnectionString"] ?? string.Empty;
    var sbQueue = builder.Configuration["ServiceBus:TicketEventsQueueName"] ?? "ticket-events";

    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    // Only wire up the Service Bus health check when a real connection string is configured.
    if (!string.IsNullOrWhiteSpace(sbConnection) && !sbConnection.StartsWith("PLACEHOLDER"))
    {
        builder.Services
            .AddHealthChecks()
            .AddAzureServiceBusQueue(sbConnection, sbQueue,
                name: "servicebus", failureStatus: HealthStatus.Degraded, tags: ["ready"]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MIDDLEWARE (registered as transient; resolved per-request by UseMiddleware<T>)
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddTransient<CorrelationIdMiddleware>();
    builder.Services.AddTransient<ExceptionHandlingMiddleware>();
    builder.Services.AddTransient<SecurityHeadersMiddleware>();

    // ─────────────────────────────────────────────────────────────────────────
    // APPLICATION + INFRASTRUCTURE
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─────────────────────────────────────────────────────────────────────────
    // BUILD
    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─────────────────────────────────────────────────────────────────────────
    // MIDDLEWARE PIPELINE — ORDER IS NOT ARBITRARY
    //
    // 1. CorrelationId        FIRST — threads the ID into LogContext for every
    //                         subsequent middleware and all log entries.
    // 2. SecurityHeaders      Applied before any other response writing.
    // 3. ExceptionHandling    Outer try/catch — wraps all business middleware.
    // 4. SerilogRequestLogging After exception handler so the logged status code
    //                         reflects the mapped code, not the raw exception.
    // 5. Swagger              Development only.
    // 6. HttpsRedirection     Redirect before auth checks.
    // 7. Authentication       MUST precede Authorization.
    // 8. Authorization
    // 9. MapControllers / MapHealthChecks
    // ─────────────────────────────────────────────────────────────────────────
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSerilogRequestLogging(o =>
    {
        o.MessageTemplate =
            "{RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.000}ms [{CorrelationId}]";
        o.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("RequestHost", ctx.Request.Host.Value);
            diag.Set("UserAgent",   ctx.Request.Headers.UserAgent.ToString());
            diag.Set("UserId",      ctx.User.FindFirst("oid")?.Value ?? "anonymous");
            if (ctx.Items.TryGetValue("CorrelationId", out var cid))
                diag.Set("CorrelationId", cid);
        };
    });

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(o =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var desc in provider.ApiVersionDescriptions)
            {
                o.SwaggerEndpoint(
                    $"/swagger/{desc.GroupName}/swagger.json",
                    $"Enterprise Ticketing API {desc.ApiVersion}");
            }
            o.OAuthClientId(app.Configuration["AzureAd:ClientId"]);
            o.OAuthUsePkce();
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready",
        new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready")
        });
    app.MapHealthChecks("/health/live",
        new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("live")
        });

    app.Run();
    return 0;
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
