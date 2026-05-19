using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Domain.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Functions.Functions;

/// <summary>
/// Azure Function triggered by messages on the ticket-events Service Bus queue.
///
/// Isolated Worker Model (.NET 10):
///   The isolated worker model runs in a separate process from the Functions host.
///   Benefits: full .NET compatibility, dependency injection, middleware, etc.
///   Unlike in-process model, no version coupling to the Functions host runtime.
///
/// Event-driven architecture benefits:
///   - Decoupled: this Function doesn't depend on the API service
///   - Independently scalable: Functions scale based on queue depth (KEDA in Container Apps)
///   - Retry resilient: Service Bus retries on failure, dead-letters after MaxDeliveryCount
///   - Observable: Application Insights captures all traces with distributed correlation
///
/// Distributed tracing:
///   The CorrelationId from the original API request flows through Service Bus
///   application properties into this Function. Application Insights shows the
///   end-to-end trace: API → Service Bus → Function as a single operation.
/// </summary>
public class TicketCreatedFunction
{
    private readonly ILogger<TicketCreatedFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TicketCreatedFunction(ILogger<TicketCreatedFunction> logger)
    {
        _logger = logger;
    }

    [Function("TicketCreatedProcessor")]
    public async Task Run(
        [ServiceBusTrigger("ticket-events", Connection = "ServiceBus__ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext executionContext)
    {
        var messageId = message.MessageId;
        var eventType = message.ApplicationProperties.TryGetValue("EventType", out var et) ? et?.ToString() : "Unknown";
        var correlationId = message.CorrelationId ?? message.MessageId;

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["EventType"] = eventType ?? "Unknown",
            ["CorrelationId"] = correlationId
        });

        try
        {
            _logger.LogInformation(
                "Processing {EventType} message {MessageId}",
                eventType, messageId);

            if (eventType == nameof(TicketCreatedEvent))
            {
                var ticketEvent = JsonSerializer.Deserialize<TicketCreatedEvent>(
                    message.Body.ToString(), JsonOptions);

                if (ticketEvent is not null)
                    await ProcessTicketCreatedAsync(ticketEvent, executionContext.CancellationToken);
            }
            else
            {
                _logger.LogWarning("Unexpected event type {EventType} — skipping", eventType);
            }

            await messageActions.CompleteMessageAsync(message, executionContext.CancellationToken);

            _logger.LogInformation("Completed message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}", messageId);

            // Dead-letter with reason for operational investigation
            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "ProcessingFailed",
                deadLetterErrorDescription: ex.Message,
                cancellationToken: executionContext.CancellationToken);
        }
    }

    private async Task ProcessTicketCreatedAsync(TicketCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing TicketCreated: {TicketNumber} (Priority: {Priority}, Customer: {CustomerId})",
            @event.TicketNumber, @event.Priority, @event.CustomerId);

        // Production implementation would:
        // 1. Send confirmation email to customer
        // 2. Start SLA timer in Azure Table Storage
        // 3. Notify assigned agent via Teams adaptive card
        // 4. Update ticket status to InProgress if auto-assign enabled
        // 5. Create ticket in external ticketing system (ServiceNow, Jira) if configured

        await Task.Delay(10, cancellationToken); // Simulate async processing
    }
}
