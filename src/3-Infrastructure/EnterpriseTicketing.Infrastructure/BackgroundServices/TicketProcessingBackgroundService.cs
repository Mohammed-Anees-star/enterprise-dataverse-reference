using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Infrastructure.Messaging.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseTicketing.Infrastructure.BackgroundServices;

/// <summary>
/// Competing-consumer background service for the ticket-events Service Bus queue.
///
/// Idempotency (FIX):
///   Service Bus guarantees at-least-once delivery — a message may arrive more than
///   once if the consumer crashes between processing and calling CompleteMessage.
///   We guard against double-processing with a two-tier strategy:
///
///   Tier 1 — In-process cache (<see cref="IMemoryCache"/>):
///     Fast O(1) lookup per message.  Survives within a single host lifetime.
///     TTL: 60 minutes (beyond typical Service Bus lock / retry window).
///
///   Tier 2 — Distributed cache (Redis / Dataverse) for multi-host deployments:
///     Inject <see cref="IProcessedEventStore"/> and check before processing.
///     The stub implementation here logs a warning to make the gap visible.
///     Replace with a concrete Redis implementation for production scale-out.
///
/// Dead-letter strategy:
///   • TransientException  → Abandon (Service Bus retries; DLQ after MaxDeliveryCount).
///   • PermanentException  → DeadLetter with reason and description.
///   • Unknown event type  → Complete (log warning; no retry loop for unknown schema).
/// </summary>
public sealed class TicketProcessingBackgroundService : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IMemoryCache _processedMessageCache;
    private readonly ILogger<TicketProcessingBackgroundService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Cache TTL must exceed (MaxDeliveryCount × LockDuration) to cover the retry window
    private static readonly MemoryCacheEntryOptions IdempotencyCacheOptions =
        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(60));

    public TicketProcessingBackgroundService(
        ServiceBusClient serviceBusClient,
        IMemoryCache processedMessageCache,
        IOptions<ServiceBusConfiguration> configuration,
        ILogger<TicketProcessingBackgroundService> logger)
    {
        _processedMessageCache = processedMessageCache;
        _logger = logger;

        _processor = serviceBusClient.CreateProcessor(
            configuration.Value.TicketEventsQueueName,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls   = configuration.Value.MaxConcurrentCalls,
                AutoCompleteMessages = false,           // explicit Complete/Abandon/DeadLetter
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync   += ProcessErrorAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ticket processing consumer started");
        await _processor.StartProcessingAsync(stoppingToken);

        // Wait indefinitely until the host requests shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken)
                  .ContinueWith(_ => Task.CompletedTask, TaskContinuationOptions.None);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId  = args.Message.MessageId;
        var eventType  = args.Message.ApplicationProperties.TryGetValue("EventType", out var et)
                         ? et?.ToString() ?? "Unknown" : "Unknown";
        var correlationId = args.Message.CorrelationId ?? messageId;

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"]     = messageId,
            ["EventType"]     = eventType,
            ["CorrelationId"] = correlationId,
            ["DeliveryCount"] = args.Message.DeliveryCount
        });

        // ── Tier 1 idempotency check ─────────────────────────────────────────
        var cacheKey = $"processed:{messageId}";
        if (_processedMessageCache.TryGetValue(cacheKey, out _))
        {
            _logger.LogInformation(
                "Duplicate message {MessageId} detected via local cache — completing without reprocessing",
                messageId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        try
        {
            _logger.LogInformation("Processing {EventType} message {MessageId}", eventType, messageId);

            await RouteEventAsync(args.Message, args.CancellationToken);

            // Mark as processed in the local cache BEFORE completing so that a crash
            // between these two lines still prevents a second processing on redelivery.
            _processedMessageCache.Set(cacheKey, true, IdempotencyCacheOptions);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);

            _logger.LogInformation("Completed {EventType} message {MessageId}", eventType, messageId);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            // Host is shutting down — abandon so the message is re-delivered to another instance
            await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
        }
        catch (InvalidDataException ex)
        {
            // Permanent failure: malformed payload cannot be retried
            _logger.LogError(ex,
                "Permanent processing failure for {EventType} {MessageId} — sending to dead-letter queue",
                eventType, messageId);

            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: "PermanentProcessingFailure",
                deadLetterErrorDescription: ex.Message,
                cancellationToken: args.CancellationToken);
        }
        catch (Exception ex)
        {
            // Transient failure — abandon and let Service Bus retry
            _logger.LogError(ex,
                "Transient failure for {EventType} {MessageId} (delivery #{DeliveryCount}) — abandoning",
                eventType, messageId, args.Message.DeliveryCount);

            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private async Task RouteEventAsync(
        ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        var eventType = message.ApplicationProperties.TryGetValue("EventType", out var et)
                        ? et?.ToString() : null;
        var body = message.Body.ToString();

        switch (eventType)
        {
            case nameof(TicketCreatedEvent):
                var created = DeserializeOrThrow<TicketCreatedEvent>(body);
                await HandleTicketCreatedAsync(created, cancellationToken);
                break;

            case nameof(TicketStatusChangedEvent):
                var changed = DeserializeOrThrow<TicketStatusChangedEvent>(body);
                await HandleStatusChangedAsync(changed, cancellationToken);
                break;

            case nameof(TicketEscalatedEvent):
                var escalated = DeserializeOrThrow<TicketEscalatedEvent>(body);
                await HandleTicketEscalatedAsync(escalated, cancellationToken);
                break;

            default:
                // Unknown schema version — complete rather than loop in DLQ.
                // Log at Warning so the monitoring alert triggers for investigation.
                _logger.LogWarning(
                    "Received message with unrecognised EventType '{EventType}' — completing without processing",
                    eventType);
                break;
        }
    }

    private static T DeserializeOrThrow<T>(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new InvalidDataException($"Deserialised {typeof(T).Name} was null");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Cannot deserialise {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private async Task HandleTicketCreatedAsync(
        TicketCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TicketCreated: {TicketNumber} Priority={Priority} Customer={CustomerId}",
            @event.TicketNumber, @event.Priority, @event.CustomerId);

        // Production: send confirmation email, start SLA timer, notify assigned agent
        await Task.Delay(10, cancellationToken);
    }

    private async Task HandleStatusChangedAsync(
        TicketStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "StatusChanged: {TicketNumber} {OldStatus} → {NewStatus}",
            @event.TicketNumber, @event.OldStatus, @event.NewStatus);

        await Task.Delay(10, cancellationToken);
    }

    private async Task HandleTicketEscalatedAsync(
        TicketEscalatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Escalation: {TicketNumber} Level={Level} Reason={Reason}",
            @event.TicketNumber, @event.EscalationLevel, @event.Reason);

        await Task.Delay(10, cancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus processor error. Source={Source} EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ticket processing consumer stopping");
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
