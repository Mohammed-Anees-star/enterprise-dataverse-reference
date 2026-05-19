using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Infrastructure.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseTicketing.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes messages from the ticket-events Service Bus queue.
///
/// Pattern: Competing Consumers — multiple instances of this service can run in parallel
/// (e.g., scaled-out App Service instances), all consuming from the same queue.
/// Service Bus ensures each message is delivered to exactly one consumer.
///
/// Idempotency consideration:
/// Service Bus guarantees at-least-once delivery. A message may be received more than once
/// if the consumer crashes after processing but before completing the message.
/// Handlers must be idempotent — processing the same event twice should be safe.
/// Strategy: Check a processed-message store (Redis/Dataverse) before processing.
///
/// Dead letter strategy:
/// After MaxDeliveryCount retries, Service Bus moves messages to the dead-letter queue.
/// A separate monitoring process reads from the DLQ, alerts ops, and optionally requeues.
/// </summary>
public sealed class TicketProcessingBackgroundService : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger<TicketProcessingBackgroundService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TicketProcessingBackgroundService(
        ServiceBusClient serviceBusClient,
        IOptions<ServiceBusConfiguration> configuration,
        ILogger<TicketProcessingBackgroundService> logger)
    {
        _logger = logger;

        var options = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = configuration.Value.MaxConcurrentCalls,
            AutoCompleteMessages = false, // Always manually complete — prevents data loss on exceptions
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        };

        _processor = serviceBusClient.CreateProcessor(
            configuration.Value.TicketEventsQueueName,
            options);

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ticket processing background service starting");
        await _processor.StartProcessingAsync(stoppingToken);

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => Task.CompletedTask, TaskContinuationOptions.None);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        var eventType = args.Message.ApplicationProperties.TryGetValue("EventType", out var et) ? et?.ToString() : "Unknown";

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["EventType"] = eventType ?? "Unknown",
            ["CorrelationId"] = args.Message.CorrelationId
        });

        try
        {
            _logger.LogInformation("Processing {EventType} message {MessageId}", eventType, messageId);

            await RouteEventAsync(args.Message, args.CancellationToken);

            // Explicit complete — message removed from queue only after successful processing
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);

            _logger.LogInformation("Completed {EventType} message {MessageId}", eventType, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {EventType} message {MessageId}", eventType, messageId);

            // Abandon returns message to queue for retry
            // After MaxDeliveryCount, Service Bus automatically moves to DLQ
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private async Task RouteEventAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        var eventType = message.ApplicationProperties.TryGetValue("EventType", out var et) ? et?.ToString() : null;
        var body = message.Body.ToString();

        switch (eventType)
        {
            case nameof(TicketCreatedEvent):
                var ticketCreated = JsonSerializer.Deserialize<TicketCreatedEvent>(body, JsonOptions);
                if (ticketCreated is not null)
                    await HandleTicketCreatedAsync(ticketCreated, cancellationToken);
                break;

            case nameof(TicketStatusChangedEvent):
                var statusChanged = JsonSerializer.Deserialize<TicketStatusChangedEvent>(body, JsonOptions);
                if (statusChanged is not null)
                    await HandleStatusChangedAsync(statusChanged, cancellationToken);
                break;

            case nameof(TicketEscalatedEvent):
                var escalated = JsonSerializer.Deserialize<TicketEscalatedEvent>(body, JsonOptions);
                if (escalated is not null)
                    await HandleTicketEscalatedAsync(escalated, cancellationToken);
                break;

            default:
                _logger.LogWarning("Received unknown event type: {EventType}", eventType);
                break;
        }
    }

    private async Task HandleTicketCreatedAsync(TicketCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing TicketCreated for {TicketNumber} (Customer: {CustomerId})",
            @event.TicketNumber, @event.CustomerId);

        // In production: send customer confirmation email, start SLA timer, notify assigned agent
        await Task.Delay(50, cancellationToken); // Simulate async work
    }

    private async Task HandleStatusChangedAsync(TicketStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing StatusChanged for {TicketNumber}: {OldStatus} → {NewStatus}",
            @event.TicketNumber, @event.OldStatus, @event.NewStatus);

        await Task.Delay(50, cancellationToken);
    }

    private async Task HandleTicketEscalatedAsync(TicketEscalatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Processing Escalation for {TicketNumber} (Level {Level}): {Reason}",
            @event.TicketNumber, @event.EscalationLevel, @event.Reason);

        await Task.Delay(50, cancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus processor error. Source: {Source}, EntityPath: {EntityPath}",
            args.ErrorSource, args.EntityPath);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ticket processing background service stopping");
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
