using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Infrastructure.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseTicketing.Infrastructure.BackgroundServices;

/// <summary>
/// Reads unpublished records from the <c>new_outboxevent</c> Dataverse table
/// and publishes them to Azure Service Bus, completing the Outbox Pattern relay.
///
/// Polling interval: 5 seconds. Tune via configuration if latency SLA is tighter.
/// Batch size: 50 records per poll.
///
/// Failure handling:
///   On publish failure the retry count is incremented in Dataverse.
///   After 10 retries the record is skipped by <see cref="IOutboxStore.GetUnpublishedAsync"/>
///   and must be investigated manually (equivalent to a dead-letter queue at the outbox level).
///
/// Idempotency:
///   <c>MessageId</c> is set to <c>EventId</c> so Service Bus deduplication (if enabled on
///   the namespace) prevents double-delivery even if the relay crashes between publish
///   and MarkPublished.
/// </summary>
public sealed class OutboxRelayBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<OutboxRelayBackgroundService> _logger;

    public OutboxRelayBackgroundService(
        IServiceScopeFactory scopeFactory,
        ServiceBusClient serviceBusClient,
        IOptions<ServiceBusConfiguration> configuration,
        ILogger<OutboxRelayBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _sender = serviceBusClient.CreateSender(configuration.Value.TicketEventsQueueName);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox relay started, polling every {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox relay encountered an unexpected error; will retry after poll interval");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }

        await _sender.DisposeAsync();
        _logger.LogInformation("Outbox relay stopped");
    }

    private async Task RelayBatchAsync(CancellationToken cancellationToken)
    {
        // IOutboxStore is Scoped; create a scope for each poll cycle
        await using var scope = _scopeFactory.CreateAsyncScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var entries = await outboxStore.GetUnpublishedAsync(BatchSize, cancellationToken);
        if (entries.Count == 0) return;

        _logger.LogDebug("Outbox relay: processing {Count} pending event(s)", entries.Count);

        foreach (var entry in entries)
        {
            try
            {
                var message = new ServiceBusMessage(entry.EventBody)
                {
                    MessageId = entry.EventId.ToString(),        // idempotency key
                    CorrelationId = entry.EventId.ToString(),
                    Subject = entry.EventType,
                    ContentType = "application/json",
                    ApplicationProperties =
                    {
                        ["EventType"] = entry.EventType,
                        ["EventVersion"] = "1.0",
                        ["OccurredAt"] = entry.OccurredAt.ToString("O")
                    }
                };

                await _sender.SendMessageAsync(message, cancellationToken);
                await outboxStore.MarkPublishedAsync(entry.OutboxEntryId, cancellationToken);

                _logger.LogInformation(
                    "Outbox relay published {EventType} (EventId={EventId})",
                    entry.EventType, entry.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to relay outbox entry {OutboxEntryId} ({EventType}); incrementing retry count",
                    entry.OutboxEntryId, entry.EventType);

                await outboxStore.IncrementRetryCountAsync(entry.OutboxEntryId, cancellationToken);
            }
        }
    }
}
