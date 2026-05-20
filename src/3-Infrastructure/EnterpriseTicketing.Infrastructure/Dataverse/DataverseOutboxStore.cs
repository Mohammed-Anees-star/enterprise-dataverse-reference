using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EnterpriseTicketing.Infrastructure.Dataverse;

/// <summary>
/// Persists domain events in the <c>new_outboxevent</c> Dataverse table.
/// The relay background service (<see cref="OutboxRelayBackgroundService"/>) reads
/// from here and publishes to Service Bus, completing the outbox pattern.
/// </summary>
public sealed class DataverseOutboxStore : IOutboxStore
{
    private const string EntityName = "new_outboxevent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ServiceClient _serviceClient;
    private readonly ILogger<DataverseOutboxStore> _logger;

    public DataverseOutboxStore(ServiceClient serviceClient, ILogger<DataverseOutboxStore> logger)
    {
        _serviceClient = serviceClient;
        _logger = logger;
    }

    public async Task AppendAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var body = JsonSerializer.Serialize((object)domainEvent, domainEvent.GetType(), JsonOptions);

        var entity = new Entity(EntityName)
        {
            ["new_eventtype"] = domainEvent.EventType,
            ["new_eventbody"] = body,
            ["new_eventid"] = domainEvent.EventId.ToString(),
            ["new_occurredat"] = domainEvent.OccurredAt.UtcDateTime,
            ["new_retrycount"] = 0
        };

        var id = await _serviceClient.CreateAsync(entity, cancellationToken);

        _logger.LogDebug(
            "Outbox event appended. EventType={EventType} EventId={EventId} OutboxId={OutboxId}",
            domainEvent.EventType, domainEvent.EventId, id);
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetUnpublishedAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryExpression(EntityName)
        {
            TopCount = batchSize,
            ColumnSet = new ColumnSet(
                "new_outboxeventid", "new_eventtype", "new_eventbody",
                "new_eventid", "new_occurredat", "new_retrycount")
        };

        // Only records where publishedat is null (not yet relayed)
        query.Criteria.AddCondition("new_publishedat", ConditionOperator.Null);
        // Avoid records that have failed many times — give up after 10 retries (goes to DLQ)
        query.Criteria.AddCondition("new_retrycount", ConditionOperator.LessThan, 10);

        query.AddOrder("new_occurredat", OrderType.Ascending);

        var result = await _serviceClient.RetrieveMultipleAsync(query, cancellationToken);

        return result.Entities.Select(e => new OutboxEntry
        {
            OutboxEntryId = e.Id,
            EventType = e.GetAttributeValue<string>("new_eventtype") ?? string.Empty,
            EventBody = e.GetAttributeValue<string>("new_eventbody") ?? string.Empty,
            EventId = Guid.TryParse(e.GetAttributeValue<string>("new_eventid"), out var g) ? g : Guid.Empty,
            OccurredAt = e.Contains("new_occurredat")
                ? new DateTimeOffset(e.GetAttributeValue<DateTime>("new_occurredat"), TimeSpan.Zero)
                : DateTimeOffset.UtcNow,
            RetryCount = e.GetAttributeValue<int>("new_retrycount")
        }).ToList().AsReadOnly();
    }

    public async Task MarkPublishedAsync(Guid outboxEntryId, CancellationToken cancellationToken = default)
    {
        var entity = new Entity(EntityName, outboxEntryId)
        {
            ["new_publishedat"] = DateTime.UtcNow
        };
        await _serviceClient.UpdateAsync(entity, cancellationToken);
    }

    public async Task IncrementRetryCountAsync(Guid outboxEntryId, CancellationToken cancellationToken = default)
    {
        // Read then write — acceptable for low-frequency failure paths
        var existing = await _serviceClient.RetrieveAsync(
            EntityName, outboxEntryId, new ColumnSet("new_retrycount"), cancellationToken);

        var currentCount = existing?.GetAttributeValue<int>("new_retrycount") ?? 0;

        var entity = new Entity(EntityName, outboxEntryId)
        {
            ["new_retrycount"] = currentCount + 1
        };
        await _serviceClient.UpdateAsync(entity, cancellationToken);
    }
}
