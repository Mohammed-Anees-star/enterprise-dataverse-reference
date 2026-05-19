# 09 - Azure Functions Flow

Isolated-worker .NET 10 host. Functions share the same Application + Infrastructure DI as the API.

| Function | Trigger | Purpose |
|---|---|---|
| `TicketCreatedFunction`     | Service Bus (ticket-events)        | Run integration side-effects after a ticket lands |
| `TicketNotificationFunction`| Service Bus (ticket-notifications) | Render template, deliver via email/SMS |
| `DataverseWebhookFunction`  | HTTP                                 | Receive Dataverse plugin callbacks |

Distributed tracing: ApplicationInsights worker telemetry preserves the W3C
TraceContext that originated in the API, so end-to-end traces (HTTP request →
Service Bus message → Function execution → downstream Dataverse call) appear as a
single transaction in the Application Map.
