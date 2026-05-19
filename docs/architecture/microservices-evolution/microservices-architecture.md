# Microservices Evolution

When (not if) the monolith outgrows a single deployable, this is the path we take.

## 1. Service Boundaries

The bounded contexts are already separated in code today; in microservices land they each
become a deployable.

| Service | Owns | Read-model surface |
|---|---|---|
| TicketService          | new_ticket, new_ticketcomment, new_ticketattachment | /api/tickets, /api/comments |
| CustomerService        | new_customer                                        | /api/customers           |
| NotificationService    | Notification outbox, templates                       | /api/notifications/status |
| IntegrationService     | External system adapters (SAP, ServiceNow, etc.)    | -                         |
| ReportingService       | CQRS read models in Cosmos / SQL                     | /api/reports             |

## 2. Communication Patterns

Synchronous (low-latency reads + commands): REST over HTTPS, fronted by APIM. All
service-to-service calls run with a managed identity and a tightly scoped audience.

Asynchronous (integration events, side-effects): Service Bus topics with per-service
subscriptions. Event schema versioned via `ApplicationProperties["Version"]`; consumers
drop unknown major versions.

## 3. API Gateway (Azure API Management)

- Edge JWT validation (offloaded from services)
- Rate limit + quota per subscription key
- Caching of GET responses (5 min default, 0s for /me endpoints)
- Backend circuit breaker (open after 50% failures in a 60s window)
- Request/response transformation policies for legacy clients

## 4. Distributed Tracing

W3C TraceContext (traceparent header) propagates through HTTP and via Service Bus
ApplicationProperties. Application Insights stitches the end-to-end trace and surfaces
it on the Application Map. Every log line carries CorrelationId, TraceId, and SpanId.

## 5. Eventual Consistency

- Outbox pattern: each service writes events to its own outbox table in the same
  transaction as the aggregate change; a background process drains the outbox to
  Service Bus.
- Saga pattern: long-running flows (e.g., "escalate ticket -> notify manager ->
  open SAP incident") modelled as state machines with compensating actions.
- Idempotent handlers: every consumer keyed by MessageId.

## 6. Service Ownership of Data

Each service owns its data store. Cross-service joins are forbidden; instead a service
maintains a materialised view of foreign data, fed by integration events. No shared
database, ever.

## 7. Scaling

- Each service is its own deployment unit on Azure Container Apps.
- KEDA scales TicketService and NotificationService on Service Bus queue length.
- ReportingService scales independently on HTTP queue length.
- Reads dominate -> add Cosmos DB replicas in customer regions; writes -> stay
  in primary region.
