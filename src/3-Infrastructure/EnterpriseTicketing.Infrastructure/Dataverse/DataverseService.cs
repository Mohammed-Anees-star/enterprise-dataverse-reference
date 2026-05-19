using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Infrastructure.Dataverse.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Polly;
using Polly.Retry;

namespace EnterpriseTicketing.Infrastructure.Dataverse;

/// <summary>
/// Implements IDataverseService using the Dataverse SDK (Microsoft.PowerPlatform.Dataverse.Client).
///
/// SDK vs Web API decision (see ADR-001):
/// The SDK is preferred when:
///   - Running within a trusted service context (not browser/mobile)
///   - Complex FetchXML queries are needed
///   - Impersonation is required
///   - Server-side plugin compatibility is needed
///   - Connection pooling via ServiceClient is beneficial
///
/// ServiceClient is thread-safe and should be registered as Singleton.
/// It maintains an internal connection pool and handles token refresh automatically.
/// </summary>
public sealed class DataverseService : IDataverseService
{
    private readonly ServiceClient _serviceClient;
    private readonly ILogger<DataverseService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public DataverseService(
        ServiceClient serviceClient,
        ILogger<DataverseService> logger,
        IOptions<DataverseConfiguration> configuration)
    {
        _serviceClient = serviceClient;
        _logger = logger;

        var config = configuration.Value;

        // Exponential backoff with jitter — prevents thundering herd on retry storms
        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryCount,
                sleepDurationProvider: (attempt, exception, context) =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 1000));
                    return baseDelay + jitter;
                },
                onRetryAsync: (exception, timespan, attempt, context) =>
                {
                    _logger.LogWarning(exception,
                        "Dataverse SDK retry attempt {Attempt} after {Delay}ms",
                        attempt, timespan.TotalMilliseconds);
                    return Task.CompletedTask;
                });
    }

    public async Task<Guid> CreateEntityAsync(
        string entityLogicalName,
        Dictionary<string, object> attributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityLogicalName);
        ArgumentNullException.ThrowIfNull(attributes);

        var entity = BuildEntity(entityLogicalName, null, attributes);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var id = await _serviceClient.CreateAsync(entity, cancellationToken);
            _logger.LogDebug("Created {EntityType} with ID {EntityId}", entityLogicalName, id);
            return id;
        });
    }

    public async Task<Dictionary<string, object>?> GetEntityAsync(
        string entityLogicalName,
        Guid id,
        IEnumerable<string> columns,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityLogicalName);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var columnSet = new ColumnSet(columns.ToArray());
            var entity = await _serviceClient.RetrieveAsync(entityLogicalName, id, columnSet, cancellationToken);

            if (entity is null) return null;

            return ExtractAttributes(entity);
        });
    }

    public async Task UpdateEntityAsync(
        string entityLogicalName,
        Guid id,
        Dictionary<string, object> attributes,
        CancellationToken cancellationToken = default)
    {
        var entity = BuildEntity(entityLogicalName, id, attributes);

        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _serviceClient.UpdateAsync(entity, cancellationToken);
            _logger.LogDebug("Updated {EntityType} {EntityId}", entityLogicalName, id);
        });
    }

    public async Task DeleteEntityAsync(
        string entityLogicalName,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _serviceClient.DeleteAsync(entityLogicalName, id, cancellationToken);
            _logger.LogDebug("Deleted {EntityType} {EntityId}", entityLogicalName, id);
        });
    }

    public async Task<(IReadOnlyList<Dictionary<string, object>> Records, string? NextPageCookie)> QueryEntitiesAsync(
        string entityLogicalName,
        string fetchXml,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fetchXml);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var fetchExpression = new FetchExpression(fetchXml);
            var result = await _serviceClient.RetrieveMultipleAsync(fetchExpression, cancellationToken);

            var records = result.Entities
                .Select(ExtractAttributes)
                .ToList()
                .AsReadOnly();

            return ((IReadOnlyList<Dictionary<string, object>>)records, result.PagingCookie);
        });
    }

    public async Task<bool> ExistsAsync(
        string entityLogicalName,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await GetEntityAsync(entityLogicalName, id, ["createdon"], cancellationToken);
        return result is not null;
    }

    private static Entity BuildEntity(string logicalName, Guid? id, Dictionary<string, object> attributes)
    {
        var entity = id.HasValue
            ? new Entity(logicalName, id.Value)
            : new Entity(logicalName);

        foreach (var (key, value) in attributes)
        {
            // Map .NET types to Dataverse SDK types
            entity[key] = value switch
            {
                Enum enumValue => new OptionSetValue(Convert.ToInt32(enumValue)),
                Guid guidValue when key.EndsWith("id", StringComparison.OrdinalIgnoreCase) =>
                    new EntityReference(DeriveRelatedEntityName(key), guidValue),
                _ => value
            };
        }

        return entity;
    }

    private static Dictionary<string, object> ExtractAttributes(Entity entity)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [$"{entity.LogicalName}id"] = entity.Id
        };

        foreach (var attr in entity.Attributes)
        {
            result[attr.Key] = attr.Value switch
            {
                OptionSetValue osv => osv.Value,
                EntityReference er => er.Id,
                Money money => money.Value,
                _ => attr.Value
            };
        }

        return result;
    }

    private static string DeriveRelatedEntityName(string attributeName)
    {
        // Convention: remove trailing "id" from lookup attribute names
        return attributeName.Length > 2
            ? attributeName[..^2]
            : attributeName;
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            || (ex.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
            || (ex.Message?.Contains("throttle", StringComparison.OrdinalIgnoreCase) == true);
    }
}
