# 03 - Request Flow

End-to-end path of a `POST /api/v1/tickets` request:

```
Client
  │  (1) HTTPS request with JWT bearer token
  ▼
Azure Front Door / APIM (production) -> WAF, rate limit, JWT pre-check
  │
  ▼
ASP.NET Core Kestrel
  │
  ▼  (2) Middleware pipeline in registration order
  ├── UseSerilogRequestLogging  -> opens log scope
  ├── CorrelationIdMiddleware   -> stamps X-Correlation-ID
  ├── SecurityHeadersMiddleware -> patches response headers
  ├── ExceptionHandlingMiddleware -> wraps everything below
  ├── UseAuthentication         -> validates JWT against Entra ID
  ├── UseAuthorization          -> applies policy attributes
  ▼
MVC routing -> TicketsController.Create([FromBody] CreateTicketRequest)
  │
  ▼  (3) Map DTO to MediatR command
TicketsController -> mediator.Send(CreateTicketCommand)
  │
  ▼  (4) Pipeline behaviours wrap the handler
UnhandledExceptionBehavior > ValidationBehavior > PerformanceBehavior > LoggingBehavior > Handler
  │
  ▼  (5) Handler orchestrates domain + persistence
CreateTicketCommandHandler
  ├── customerRepository.ExistsAsync(...)
  ├── Ticket.Create(...)                    -> raises TicketCreatedEvent in entity
  ├── ticketRepository.AddAsync(ticket)     -> Dataverse SDK CreateAsync
  ├── eventBus.PublishAsync(TicketCreated)  -> Service Bus
  └── ticket.ClearDomainEvents()
  │
  ▼  (6) Result bubbles back up
Handler -> ActionResult<Guid> -> ProducesResponseType(201)
  │
  ▼
HTTP 201 Created + Location header back to client
```

Cross-cutting:
- Correlation ID flows through every step in `LogContext`.
- Cancellation token from `HttpContext.RequestAborted` propagates the whole way.
- Application Insights collects: request, dependency, exception telemetry.
