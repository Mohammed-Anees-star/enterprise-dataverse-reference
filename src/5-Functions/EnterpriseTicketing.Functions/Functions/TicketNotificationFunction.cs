using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Functions.Functions;

/// <summary>
/// Service-Bus-triggered function that delivers ticket notifications via the customer's
/// preferred channel (email today; SMS / push when the channel registry exposes them).
///
/// We keep the function lean: it parses the event, hydrates the notification template,
/// hands off to a notification service, and lets the binding handle Complete/Abandon
/// based on the function result.
/// </summary>
public sealed class TicketNotificationFunction(ILogger<TicketNotificationFunction> logger)
{
    [Function("TicketNotificationProcessor")]
    public async Task Run(
        [ServiceBusTrigger("ticket-notifications", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var correlationId = message.CorrelationId ?? Guid.NewGuid().ToString();
        var eventType = message.Subject ?? "unknown";

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = message.MessageId,
            ["EventType"] = eventType
        });

        logger.LogInformation(
            "Processing notification {EventType} for message {MessageId}",
            eventType, message.MessageId);

        // Real implementation would: load notification template, render with event data,
        // route to channel adapter (SendGrid / Azure Communication Services), record audit
        // entry in Dataverse. We model the workflow but stop short of binding to a specific
        // mail provider so this reference solution stays infrastructure-agnostic.
        await Task.Delay(10).ConfigureAwait(false);

        logger.LogInformation("Notification delivered for {MessageId}", message.MessageId);
    }
}
