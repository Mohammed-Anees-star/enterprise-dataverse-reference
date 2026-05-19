# 08 - Service Bus Flow

```
Publisher (API or Function)
   │  PublishAsync<TicketCreatedEvent>
   ▼
ServiceBusEventBus
   ├─ MessageId = Guid.NewGuid()         (idempotency key)
   ├─ CorrelationId = current trace id
   ├─ Subject = nameof(TicketCreatedEvent)
   ├─ ApplicationProperties["EventType"] = T.FullName
   ▼
ServiceBusSender.SendMessageAsync (with Polly retry)
   │
   ▼
Azure Service Bus queue: ticket-events
   │
   ▼
ServiceBusProcessor (in-process consumer)
   │  PeekLock, MaxDeliveryCount=10
   │  ProcessMessageAsync
   │      ├─ check idempotency cache (MessageId) -> Complete if dup
   │      ├─ dispatch via reflected handler
   │      ├─ Complete on success
   │      ├─ Abandon (transient) -> auto-retry
   │      └─ DeadLetter (poison) after exhaustion
   ▼
Downstream: domain handlers, notifications, integrations
```

DLQ strategy: a periodic background service inspects the DLQ every 5 minutes,
emits an Application Insights custom event per poison message, and (optionally)
requeues messages that failed for known-transient reasons.
