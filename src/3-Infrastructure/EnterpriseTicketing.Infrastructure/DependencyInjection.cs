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
/// Infrastructure layer DI registration.
/// All infrastructure implementations are wired here — the API/API layer never
/// references concrete Infrastructure types directly (only interfaces from Application/Domain).
///
/// Registration strategy:
///   - ServiceClient: Singleton (thread-safe, connection-pooled)
///   - DataverseService: Singleton (wraps singleton ServiceClient)
///   - Repositories: Scoped (per-request, safe for request-scoped logging context)
///   - ServiceBusClient: Singleton (SDK best practice — expensive to create)
///   - ServiceBusEventBus: Scoped (creates sender per scope, disposed properly)
///   - Background services: Singleton (long-running host services)
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate configuration at startup — fail fast on missing config
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
        // ServiceClient is thread-safe and expensive to create — must be Singleton
        services.AddSingleton<ServiceClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<DataverseConfiguration>>().Value;
            var logger = sp.GetRequiredService<ILogger<ServiceClient>>();

            var connectionString =
                $"AuthType=ClientSecret;" +
                $"Url={config.Url};" +
                $"TenantId={config.TenantId};" +
                $"ClientId={config.ClientId};" +
                $"ClientSecret={config.ClientSecret};";

            var client = new ServiceClient(connectionString, logger);

            if (!client.IsReady)
                throw new InvalidOperationException(
                    $"Dataverse ServiceClient failed to initialize: {client.LastError}");

            return client;
        });

        services.AddSingleton<IDataverseService, DataverseService>();
    }

    private static void AddDataverseWebApi(IServiceCollection services, IConfiguration configuration)
    {
        var dataverseUrl = configuration[$"{DataverseConfiguration.SectionName}:Url"]
            ?? "https://placeholder.crm.dynamics.com";

        services.AddSingleton<IDataverseTokenProvider, DataverseTokenProvider>();
        services.AddTransient<DataverseHttpClientHandler>();

        // Named HttpClient with Polly policies and DelegatingHandler
        services.AddHttpClient("DataverseWebApi", client =>
        {
            client.BaseAddress = new Uri($"{dataverseUrl.TrimEnd('/')}/api/data/v9.2/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<DataverseHttpClientHandler>();
        // Note: In production, add Polly policies here via .AddPolicyHandler()

        services.AddScoped<IDataverseWebApiService, DataverseWebApiService>();
    }

    private static void AddMessaging(IServiceCollection services)
    {
        services.AddSingleton<ServiceBusClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value;
            // In production: use Managed Identity instead of connection string
            // return new ServiceBusClient(config.FullyQualifiedNamespace, new DefaultAzureCredential());
            return new ServiceBusClient(config.ConnectionString);
        });

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
        services.AddScoped<EnterpriseTicketing.Domain.Interfaces.ITicketRepository, TicketRepository>();
        services.AddScoped<EnterpriseTicketing.Domain.Interfaces.ICustomerRepository, CustomerRepository>();
    }

    private static void AddBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<TicketProcessingBackgroundService>();
    }
}
