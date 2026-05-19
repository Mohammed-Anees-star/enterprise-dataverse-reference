namespace EnterpriseTicketing.API.Configuration;

/// <summary>
/// Health checks split into three classes:
///   /health/live  - process is up (no dependency checks; for liveness probes)
///   /health/ready - dependencies reachable; tagged with "ready" so the readiness
///                   probe in Kubernetes / App Service routes traffic only when true
///   /health       - everything (for dashboards)
/// </summary>
public static class HealthCheckConfiguration
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var hcBuilder = services.AddHealthChecks();

        var serviceBusConn = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(serviceBusConn)
            && !serviceBusConn.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            hcBuilder.AddAzureServiceBusQueue(
                connectionStringFactory: _ => serviceBusConn,
                queueNameFactory: _ => configuration["ServiceBus:TicketEventsQueueName"] ?? "ticket-events",
                name: "service-bus-tickets",
                tags: ["ready", "messaging"]);
        }

        // Dataverse health check lives in Infrastructure but is registered here for clarity
        hcBuilder.AddCheck("dataverse-config", () =>
        {
            var url = configuration["Dataverse:Url"];
            return string.IsNullOrWhiteSpace(url) || url.Contains("YOUR_ORG", StringComparison.Ordinal)
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Dataverse URL not configured for this environment.")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
        }, tags: ["ready", "dataverse"]);

        return services;
    }
}
