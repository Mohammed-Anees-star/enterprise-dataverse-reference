using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Infrastructure.Messaging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseTicketing.Infrastructure.Messaging;

/// <summary>
/// Implements IEventBus using Azure Service Bus.
///
/// Why Azure Service Bus over direct HTTP callbacks or in-process events:
///   1. Guaranteed delivery — messages persist until explicitly completed
///   2. Retry semantics — failed processing retried automatically
///   3. Dead-letter queue — poison messages isolated for investigation
///   4. Decoupling — publisher doesn't know about consumers
///   5. Load leveling — consumers process at their own rate
///   6. Session support — ordered processing of related messages
///   7. Scheduled delivery — delay message processing
///
/// MessageId is set to prevent duplicate processing (idempotency).
/// CorrelationId threads the trace across service boundaries.
/// Subject carries the event type for efficient routing without deserialization.
/// </summary>
public sealed class ServiceBusEventBus : IEventBus, IAsyncDisposable
{
    private readonly ServiceBusSender _ticketEventsSender;
    private readonly ILogger<ServiceBusEventBus> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public ServiceBusEventBus(
        ServiceBusClient serviceBusClient,
        IOptions<ServiceBusConfiguration> configuration,
        ILogger<ServiceBusEventBus> logger)
    {
        _logger = logger;
        _ticketEventsSender = serviceBusClient.CreateSender(configuration.Value.TicketEventsQueueName);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventBody = JsonSerializer.Serialize(@event, JsonOptions);
        var eventType = @event.GetType().Name;

        var message = new ServiceBusMessage(eventBody)
        {
            // Unique message ID for idempotency — prevents duplicate processing on redelivery
            MessageId = @event.EventId.ToString(),
            CorrelationId = @event.EventId.ToString(),
            Subject = eventType,
            ContentType = "application/json",
            ApplicationProperties =
            {
                ["EventType"] = eventType,
                ["EventVersion"] = "1.0",
                ["OccurredAt"] = @event.OccurredAt.ToString("O")
            }
        };

        await _ticketEventsSender.SendMessageAsync(message, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} (MessageId: {MessageId}) to Service Bus",
            eventType, message.MessageId);
    }

    public async ValueTask DisposeAsync()
    {
        await _ticketEventsSender.DisposeAsync();
    }
}
