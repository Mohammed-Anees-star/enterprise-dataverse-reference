using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Infrastructure.BackgroundServices;
using EnterpriseTicketing.Infrastructure.Dataverse;
using EnterpriseTicketing.Infrastructure.Dataverse.Configuration;
using EnterpriseTicketing.Infrastructure.Dataverse.Repositories;
using EnterpriseTicketing.Infrastructure.Http;
using EnterpriseTicketing.Infrastructure.Messaging;
using EnterpriseTicketing.Infrastructure.Messaging.Configuration;
using EnterpriseTicketing.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace EnterpriseTicketing.Infrastructure;

/// <summary>
/// Infrastructure layer service registrations.
///
/// Lifetime strategy:
///   Singleton  — ServiceClient, ServiceBusClient, IDataverseTokenProvider, IMemoryCache
///                (expensive to create; thread-safe; long-lived)
///   Scoped     — Repositories, IDataverseWebApiService, IOutboxStore, IEventBus
///                (per-request; safe to hold request-scoped data / logger context)
///   Transient  — DataverseHttpClientHandler
///                (HttpMessageHandler; new instance per HttpClient pipeline creation)
///   Hosted     — BackgroundService registrations (IHostedService / singleton by framework)
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate at startup — fail fast rather than at first request
        services.AddOptions<DataverseConfiguration>()
            .Bind(configuration.GetSection(DataverseConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ServiceBusConfiguration>()
            .Bind(configuration.GetSection(ServiceBusConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddDataverseSdk(services);
        AddDataverseWebApi(services, configuration);
        AddMessaging(services);
        AddSecurity(services);
        AddRepositories(services);
        AddBackgroundServices(services);

        return services;
    }

    private static void AddDataverseSdk(IServiceCollection services)
    {
        // ServiceClient is thread-safe and connection-pooled — must be Singleton
        services.AddSingleton<ServiceClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<DataverseConfiguration>>().Value;
            var logger = sp.GetRequiredService<ILogger<ServiceClient>>();

            // For production: replace with Managed Identity
            //   var credential = new DefaultAzureCredential();
            //   var client = new ServiceClient(new Uri(config.Url), credential, logger);
            var connectionString =
                $"AuthType=ClientSecret;" +
                $"Url={config.Url};" +
                $"TenantId={config.TenantId};" +
                $"ClientId={config.ClientId};" +
                $"ClientSecret={config.ClientSecret};";

            var client = new ServiceClient(connectionString, logger);

            // IsReady check is non-blocking — connection is verified lazily on first call
            // We log rather than throw here so the process starts in degraded-mode
            // rather than crashing entirely; the /health/ready probe will surface the fault.
            if (!client.IsReady)
                logger.LogError("Dataverse ServiceClient is NOT ready: {LastError}", client.LastError);

            return client;
        });

        services.AddSingleton<IDataverseService, DataverseService>();
        services.AddSingleton<ITicketNumberSequence, DataverseTicketNumberSequence>();
        services.AddScoped<IOutboxStore, DataverseOutboxStore>();
    }

    private static void AddDataverseWebApi(IServiceCollection services, IConfiguration configuration)
    {
        var dataverseUrl = configuration[$"{DataverseConfiguration.SectionName}:Url"]
                           ?? "https://placeholder.crm.dynamics.com";

        services.AddSingleton<IDataverseTokenProvider, DataverseTokenProvider>();
        services.AddTransient<DataverseHttpClientHandler>();

        services.AddHttpClient("DataverseWebApi", client =>
            {
                client.BaseAddress = new Uri($"{dataverseUrl.TrimEnd('/')}/api/data/v9.2/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<DataverseHttpClientHandler>();

        services.AddScoped<IDataverseWebApiService, DataverseWebApiService>();
    }

    private static void AddMessaging(IServiceCollection services)
    {
        services.AddSingleton<ServiceBusClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value;

            // Guard against placeholder values so the app starts without a real connection string
            if (string.IsNullOrWhiteSpace(config.ConnectionString)
                || config.ConnectionString.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                sp.GetRequiredService<ILogger<ServiceBusClient>>()
                  .LogWarning("ServiceBus ConnectionString is a placeholder — messaging is disabled");
                // Return a no-op client that accepts the namespace value as a FQDN
                return new ServiceBusClient("placeholder.servicebus.windows.net");
            }

            return new ServiceBusClient(config.ConnectionString);
        });

        // IEventBus is still wired to ServiceBusEventBus for direct publish scenarios
        services.AddScoped<IEventBus, ServiceBusEventBus>();
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddMemoryCache();
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<Domain.Interfaces.ITicketRepository, TicketRepository>();
        services.AddScoped<Domain.Interfaces.ICustomerRepository, CustomerRepository>();
    }

    private static void AddBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<TicketProcessingBackgroundService>();
        services.AddHostedService<OutboxRelayBackgroundService>();
    }
}
