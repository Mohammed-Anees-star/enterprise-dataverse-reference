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
/// Implements <see cref="IDataverseService"/> using the Dataverse SDK.
///
/// Attribute mapping rules (all bugs fixed):
///
///   PRIMARY KEY columns (e.g., new_ticketid):
///     Written as raw Guid — the SDK maps these automatically because they match
///     the Entity's primary-key attribute.  Converting them to EntityReference
///     would cause a Dataverse error ("cannot set primary key via reference").
///
///   LOOKUP columns (e.g., new_customerid):
///     Must be wrapped in EntityReference with the related entity's logical name.
///     The caller passes a <see cref="LookupValue"/> marker (see below) to distinguish
///     a lookup Guid from an ordinary Guid attribute.
///
///   OPTION SET columns (enum values):
///     Wrapped in <see cref="OptionSetValue"/>.
///     Caller passes the raw int (cast from the enum).
///
///   All other types: passed as-is.
///
/// FetchXML injection safety:
///   All caller-supplied string values that flow into dynamic FetchXML conditions
///   must be passed through <see cref="XmlEscape"/> before interpolation.
///   Numeric/Guid values do not need escaping.
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

        _retryPolicy = Policy
            .Handle<Exception>(IsTransientException)
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryCount,
                sleepDurationProvider: (attempt, _, _) =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)),
                onRetryAsync: (exception, timespan, attempt, _) =>
                {
                    _logger.LogWarning(exception,
                        "Dataverse SDK retry {Attempt}/{Max} after {Delay:F0}ms",
                        attempt, config.MaxRetryCount, timespan.TotalMilliseconds);
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

        var entity = BuildEntity(entityLogicalName, id: null, attributes);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var id = await _serviceClient.CreateAsync(entity, cancellationToken);
            _logger.LogDebug("Created {EntityType} Id={EntityId}", entityLogicalName, id);
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
            return entity is null ? null : ExtractAttributes(entity);
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
            _logger.LogDebug("Updated {EntityType} Id={EntityId}", entityLogicalName, id);
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
            _logger.LogDebug("Deleted {EntityType} Id={EntityId}", entityLogicalName, id);
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
            var result = await _serviceClient.RetrieveMultipleAsync(
                new FetchExpression(fetchXml), cancellationToken);

            var records = result.Entities
                .Select(ExtractAttributes)
                .ToList()
                .AsReadOnly();

            return ((IReadOnlyList<Dictionary<string, object>>)records, result.PagingCookie);
        });
    }

    public async Task<(IReadOnlyList<Dictionary<string, object>> Records, int TotalRecordCount, string? NextPageCookie)>
        QueryEntitiesWithCountAsync(
            string entityLogicalName,
            string fetchXml,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fetchXml);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var result = await _serviceClient.RetrieveMultipleAsync(
                new FetchExpression(fetchXml), cancellationToken);

            var records = result.Entities
                .Select(ExtractAttributes)
                .ToList()
                .AsReadOnly();

            return (
                (IReadOnlyList<Dictionary<string, object>>)records,
                result.TotalRecordCount,         // accurate count when returntotalrecordcount="true"
                result.PagingCookie);
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

    // -------------------------------------------------------------------------
    // Attribute building — critical correctness section
    // -------------------------------------------------------------------------

    private static Entity BuildEntity(string logicalName, Guid? id, Dictionary<string, object> attributes)
    {
        var entity = id.HasValue
            ? new Entity(logicalName, id.Value)
            : new Entity(logicalName);

        foreach (var (key, value) in attributes)
        {
            entity[key] = MapToSdkValue(key, value);
        }

        return entity;
    }

    /// <summary>
    /// Maps a caller-supplied attribute value to the correct Dataverse SDK type.
    ///
    /// IMPORTANT — why we require explicit <see cref="LookupValue"/> for lookups:
    ///   A Guid can represent either:
    ///     (a) a primary-key attribute (e.g., new_ticketid) — raw Guid
    ///     (b) a lookup attribute (e.g., new_customerid) — EntityReference
    ///   We cannot infer intent from the column name alone (heuristics break on
    ///   custom schemas). Callers must tag lookup Guids with <see cref="LookupValue"/>.
    /// </summary>
    private static object MapToSdkValue(string attributeName, object value) => value switch
    {
        LookupValue lookup =>
            new EntityReference(lookup.EntityLogicalName, lookup.Id),
        OptionSetValue osv => osv,           // already correct SDK type
        int intValue when IsOptionSetColumn(attributeName) =>
            new OptionSetValue(intValue),
        _ => value
    };

    /// <summary>
    /// Heuristic: attributes whose names end with "_status", "_priority", "_category"
    /// (the columns we control) are OptionSet columns.  For everything else we pass
    /// the value as-is and let the SDK validate.
    /// </summary>
    private static bool IsOptionSetColumn(string attributeName) =>
        attributeName.EndsWith("_status", StringComparison.OrdinalIgnoreCase) ||
        attributeName.EndsWith("_priority", StringComparison.OrdinalIgnoreCase) ||
        attributeName.EndsWith("_category", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object> ExtractAttributes(Entity entity)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            // Always include the primary key as a raw Guid
            [$"{entity.LogicalName}id"] = entity.Id
        };

        foreach (var attr in entity.Attributes)
        {
            result[attr.Key] = attr.Value switch
            {
                OptionSetValue osv  => osv.Value,     // return int for enum reconstruction
                EntityReference er  => er.Id,         // return Guid; caller maps to domain type
                Money money         => money.Value,
                AliasedValue av     => av.Value,      // from linked-entity aliases
                _                   => attr.Value
            };
        }

        return result;
    }

    private static bool IsTransientException(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException
        || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("throttle", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // FetchXML injection safety
    // -------------------------------------------------------------------------

    /// <summary>
    /// XML-escapes a string value before embedding it in a FetchXML condition.
    /// Must be applied to all user-supplied or externally-sourced string values.
    /// Guid and integer values do not need escaping.
    /// </summary>
    public static string XmlEscape(string value) =>
        System.Security.SecurityElement.Escape(value) ?? string.Empty;
}

// -------------------------------------------------------------------------
// Marker type for lookup attributes
// -------------------------------------------------------------------------

/// <summary>
/// Marks a Guid attribute as a Dataverse lookup that should become an
/// <see cref="EntityReference"/> when written to the SDK.
/// </summary>
/// <param name="EntityLogicalName">The related entity's logical name.</param>
/// <param name="Id">The related record's ID.</param>
public sealed record LookupValue(string EntityLogicalName, Guid Id);
