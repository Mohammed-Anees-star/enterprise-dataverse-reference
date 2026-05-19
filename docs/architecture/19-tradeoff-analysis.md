# 19 - Tradeoff Analysis (ADRs)

## ADR-001: Dataverse SDK as primary, Web API as secondary

Status: Accepted

Context: The SDK is the canonical, type-safe access path; the Web API is the
HTTP-native OData surface. Both expose the same data but with different ergonomics.

Decision: Use the SDK for all internal server-side calls. Reserve the Web API for:
- Cross-language microservice clients
- Batch ($batch) operations
- OData query semantics that FetchXML cannot express cleanly

Consequences:
+ Best ergonomics in the .NET layer.
+ One canonical retry/circuit-breaker policy (SDK + HTTP both wrapped by Polly).
- Web API code path is exercised less; we mitigate with a small integration test.

## ADR-002: Synchronous command + asynchronous integration events

Status: Accepted

Context: Customers expect "ticket created" to return a 201 with an id; downstream
side-effects (email, Teams alert) can run asynchronously.

Decision: Persist + return synchronously. Publish integration events to Service Bus
after the persistence succeeds. Subscribers handle email, notifications, integrations.

Consequences:
+ Predictable client UX.
+ Side-effects scale independently of the API.
- Possibility of a window where the ticket exists but no event has been published.
  Mitigated by the outbox pattern roadmap (see 10-event-driven-architecture.md).

## ADR-003: Monolith first, microservices when needed

Status: Accepted

Context: A single bounded context fits in one solution. Premature service
decomposition is the most expensive mistake we can make.

Decision: Ship as a Clean-Architecture monolith. The bounded contexts (Ticket,
Customer, Notification, Reporting) live in separate folders/projects so that any
future split is mechanical, not a re-architecture. See `microservices-evolution/`.
