# Enterprise Ticketing — Architecture Documentation

> Written as a senior architect mentoring an engineering team.

---

## Table of Contents

1. [Full Architecture Overview](#1-full-architecture-overview)
2. [Clean Architecture Explanation](#2-clean-architecture-explanation)
3. [Request Flow End-to-End](#3-request-flow-end-to-end)
4. [Authentication Flow](#4-authentication-flow)
5. [Dataverse SDK vs Web API](#5-dataverse-sdk-vs-web-api)
6. [Power Platform Integration](#6-power-platform-integration)
7. [Azure Service Bus Flow](#7-azure-service-bus-flow)
8. [Azure Functions Flow](#8-azure-functions-flow)
9. [Event-Driven Architecture](#9-event-driven-architecture)
10. [Dependency Injection](#10-dependency-injection)
11. [Middleware Pipeline](#11-middleware-pipeline)
12. [Logging Architecture](#12-logging-architecture)
13. [Exception Handling Strategy](#13-exception-handling-strategy)
14. [Configuration Strategy](#14-configuration-strategy)
15. [Security Architecture](#15-security-architecture)
16. [Scalability Considerations](#16-scalability-considerations)
17. [Production Support](#17-production-support)
18. [Tradeoff Analysis](#18-tradeoff-analysis)
19. [Enterprise vs Junior Patterns](#19-enterprise-vs-junior-patterns)
20. [Evolution Roadmap](#20-evolution-roadmap)

---

## 1. Full Architecture Overview

```
                           ┌─────────────────────────────────────────────────────┐
                           │              External Clients                        │
                           │   (Web Apps, Mobile, Third-party integrations)       │
                           └────────────────────┬────────────────────────────────┘
                                                │ HTTPS + JWT Bearer
                           ┌────────────────────▼────────────────────────────────┐
                           │         Azure API Management (APIM)                 │
                           │   Rate limiting | Auth offload | Caching | Analytics │
                           └────────────────────┬────────────────────────────────┘
                                                │
                           ┌────────────────────▼────────────────────────────────┐
                           │     ASP.NET Core Web API (.NET 10)                  │
                           │   Azure App Service (P2v3, deployment slots)         │
                           │                                                     │
                           │  ┌───────────────────────────────────────────────┐  │
                           │  │  Middleware Pipeline                          │  │
                           │  │  CorrelationId → Security Headers →           │  │
                           │  │  Exception Handler → Serilog Logging →        │  │
                           │  │  Auth/AuthZ → Controllers                     │  │
                           │  └──────────────┬────────────────────────────────┘  │
                           │                 │ MediatR                           │
                           │  ┌──────────────▼────────────────────────────────┐  │
                           │  │  Application Layer (CQRS)                     │  │
                           │  │  Commands | Queries | Pipeline Behaviors       │  │
                           │  └──────────────┬────────────────────────────────┘  │
                           │                 │ Repository Interfaces             │
                           │  ┌──────────────▼────────────────────────────────┐  │
                           │  │  Infrastructure Layer                         │  │
                           │  │  Dataverse SDK | Web API | Service Bus        │  │
                           │  └──────────────┬────────────────────────────────┘  │
                           └─────────────────┼───────────────────────────────────┘
                                             │
                     ┌───────────────────────┼───────────────────────────┐
                     │                       │                           │
        ┌────────────▼──────────┐  ┌─────────▼──────────┐  ┌───────────▼──────────┐
        │  Microsoft Dataverse  │  │  Azure Service Bus  │  │ Application Insights │
        │  (Power Platform)     │  │  Queues + Topics    │  │ Distributed Tracing  │
        └────────────┬──────────┘  └─────────┬──────────┘  └──────────────────────┘
                     │                       │
        ┌────────────▼──────────┐  ┌─────────▼──────────┐
        │  Model-Driven App     │  │  Azure Functions    │
        │  (Operations Staff)   │  │  Event Processors   │
        └───────────────────────┘  └────────────────────┘
                                             │
                                   ┌─────────▼──────────┐
                                   │  Azure Key Vault    │
                                   │  (Secrets via MI)   │
                                   └────────────────────┘
```

---

## 2. Clean Architecture Explanation

Clean Architecture (Robert C. Martin) organizes code into concentric layers with one rule:
**Dependencies point inward only.** Inner layers know nothing about outer layers.

```
         ┌─────────────────────────────┐
         │          4-API              │  Controllers, Middleware, Startup
         │  ┌───────────────────────┐  │
         │  │    3-Infrastructure   │  │  Dataverse SDK, Service Bus, MSAL
         │  │  ┌─────────────────┐  │  │
         │  │  │  2-Application  │  │  │  CQRS, Validators, DTOs
         │  │  │  ┌───────────┐  │  │  │
         │  │  │  │ 1-Domain  │  │  │  │  Entities, Events, Value Objects
         │  │  │  └───────────┘  │  │  │
         │  │  └─────────────────┘  │  │
         │  └───────────────────────┘  │
         └─────────────────────────────┘
```

### Layer Responsibilities

| Layer | What lives here | External dependencies |
|-------|----------------|----------------------|
| Domain | Entities, Value Objects, Domain Events, Repository *interfaces* | **None** |
| Application | Commands, Queries, Handlers, Validators, Application *interfaces* | Domain only |
| Infrastructure | Repository *implementations*, Dataverse SDK, HTTP clients, Service Bus | Application + Domain |
| API | Controllers, Middleware, DI bootstrap, Swagger, Auth config | All layers |

### Why this works

A junior engineer asks: *"Why can't I just call `ServiceClient` from my controller?"*

Because:
1. **Testability**: You cannot unit test a controller that calls Dataverse. With interfaces and DI, you test the handler in isolation with a mock.
2. **Changeability**: If Microsoft changes the Dataverse SDK API, you change only `DataverseService.cs` — nothing else.
3. **Understandability**: `CreateTicketCommandHandler` reads like a business process. It tells you *what* happens, not *how* the database works.
4. **Scalability of teams**: Domain team works on business rules. API team works on contracts. Infrastructure team optimizes Dataverse queries. All in parallel, without stepping on each other.

---

## 3. Request Flow End-to-End

`POST /api/v1/tickets` with JWT token and JSON body:

```
1. HTTPS arrives at Azure App Service
2. CorrelationIdMiddleware → extracts/generates X-Correlation-ID, adds to LogContext
3. SecurityHeadersMiddleware → adds X-Content-Type-Options, removes Server header
4. ExceptionHandlingMiddleware → wraps remaining pipeline in try/catch
5. Serilog UseSerilogRequestLogging → starts timing the request
6. UseAuthentication → validates JWT Bearer token against Azure AD JWKS endpoint
7. UseAuthorization → checks [Authorize] attribute and policy claims
8. TicketsController.CreateTicket() → called with deserialized CreateTicketRequest
9. Controller maps request → CreateTicketCommand
10. ISender.Send(command) → dispatches to MediatR
11. MediatR Pipeline Behavior 1: UnhandledExceptionBehavior.Handle()
12. MediatR Pipeline Behavior 2: ValidationBehavior.Handle()
    → CreateTicketCommandValidator.ValidateAsync(command) — all rules pass
13. MediatR Pipeline Behavior 3: PerformanceBehavior.Handle() — starts stopwatch
14. MediatR Pipeline Behavior 4: LoggingBehavior.Handle() — logs "Handling CreateTicketCommand"
15. CreateTicketCommandHandler.Handle()
    a. customerRepository.ExistsAsync(customerId) → TicketRepository → DataverseService.ExistsAsync()
       → ServiceClient.RetrieveAsync() → Dataverse API call (with Polly retry if needed)
    b. Ticket.Create() → domain factory, raises TicketCreatedEvent
    c. ticketRepository.AddAsync(ticket) → DataverseService.CreateEntityAsync()
       → ServiceClient.CreateAsync() → Dataverse API call
    d. eventBus.PublishAsync(TicketCreatedEvent) → ServiceBusEventBus.PublishAsync()
       → ServiceBusSender.SendMessageAsync() → Azure Service Bus
16. Handler returns Guid (new ticket ID)
17. LoggingBehavior logs "Handled CreateTicketCommand"
18. PerformanceBehavior checks elapsed time
19. Controller: return CreatedAtAction(GetTicket, new { id = ticketId })
20. Serilog logs: POST /api/v1/tickets 201 in 45.123ms | CorrelationId: abc-123
21. Response: 201 Created with Location header
```

This flow is deterministic, testable at every layer, and fully observable.

---

## 4. Authentication Flow

```
Client                Azure AD                  ASP.NET Core API
  │                      │                            │
  │─── POST /token ──────►│                            │
  │    client_id          │                            │
  │    client_secret      │                            │
  │    scope: api://...   │                            │
  │◄── access_token ──────│                            │
  │                       │                            │
  │─── GET /api/v1/tickets ────────────────────────────►│
  │    Authorization: Bearer {jwt}                     │
  │                       │ validate token (JWKS)      │
  │                       │◄──────────────────────────►│
  │                       │                            │
  │◄── 200 OK ─────────────────────────────────────────│
```

**JWT validation process** (done by ASP.NET Core + Microsoft.Identity.Web):
1. Decode JWT header → find `kid` (key ID)
2. Fetch JWKS from `https://login.microsoftonline.com/{tenant}/discovery/v2.0/keys`
3. Verify signature using public key matching `kid`
4. Check `aud` claim matches API's Client ID
5. Check `iss` claim matches tenant
6. Check `exp` claim — token not expired
7. Extract claims: `oid` (user ID), `name`, `preferred_username`, `roles`

**Dataverse OAuth** (separate from API auth):
- API authenticates TO Dataverse using its own Client ID + Secret (MSAL client credentials)
- Scope: `https://org.crm.dynamics.com/.default`
- Token cached by DataverseTokenProvider (5 min buffer before expiry)

---

## 5. Dataverse SDK vs Web API

### Use SDK (Microsoft.PowerPlatform.Dataverse.Client) when:

| Scenario | Why SDK wins |
|----------|-------------|
| Server-side processing in .NET | Connection pooling, type safety |
| Complex FetchXML queries | FetchXML is more powerful than OData for complex joins |
| Server-side plugins/webhooks | SDK is required for plugin execution context |
| Impersonation (CallerObjectId) | Native SDK support |
| Bulk operations | SDK handles batching more naturally |
| Offline/cached metadata | SDK caches entity metadata |

### Use Web API (OData/REST) when:

| Scenario | Why Web API wins |
|----------|----------------|
| Non-.NET services | Language-agnostic JSON/REST |
| Microservices needing independence | No SDK version coupling |
| Power Automate custom connectors | OData is the native format |
| Cross-platform mobile apps | HTTP/JSON native |
| $batch operations | Standard OData protocol |
| When SDK adds too much binary size | Lambda/Functions cold start concern |
| Change tracking with ETag | Standard HTTP semantics |

**This solution uses SDK as primary** for the main API (server-side .NET context, connection pooling benefit) and Web API in `DataverseWebApiService` for use cases requiring HTTP-native patterns.

---

## 7. Azure Service Bus Flow

```
API Handler                Service Bus               Background Service
    │                          │                            │
    │─ PublishAsync(event) ────►│ ticket-events queue        │
    │  MessageId: {guid}        │ MessageId, Subject,        │
    │  Subject: TicketCreated   │ CorrelationId, Body(JSON)  │
    │                          │                            │
    │                          │◄─ Receive (AutoComplete:false)
    │                          │                            │
    │                          │───── Deliver message ──────►│
    │                          │                            │ Deserialize
    │                          │                            │ Route by Subject
    │                          │                            │ Process (idempotent)
    │                          │                            │
    │                          │◄── CompleteMessage() ──────│ (success)
    │                          │         OR                 │
    │                          │◄── AbandonMessage() ───────│ (transient failure)
    │                          │  retry count++             │
    │                          │         OR                 │
    │                          │◄── DeadLetterMessage() ────│ (after MaxDeliveryCount)
    │                          │  DLQ monitoring alert      │
```

**Dead Letter Queue strategy:**
- After 10 failed deliveries (configurable via `maxDeliveryCount`), Service Bus moves message to `ticket-events/$deadletterqueue`
- DLQ is monitored via Azure Monitor alerts
- On-call engineer investigates root cause
- Valid messages may be requeued after fix deployment
- Poison messages (schema mismatch, bad data) are archived and discarded

**Idempotency:**
- Every message has a unique `MessageId` (Event GUID)
- Process message → check if EventId already in processed-events store
- If already processed: complete immediately (skip reprocessing)
- Store: Redis distributed cache or Dataverse table

---

## 11. Middleware Pipeline

**Order matters. Wrong order = subtle production bugs.**

```
Request ──►
  CorrelationIdMiddleware        ← Must be FIRST: threads CorrelationId through all subsequent middleware
  SecurityHeadersMiddleware      ← Headers on ALL responses
  ExceptionHandlingMiddleware    ← Catches all downstream exceptions
  SerilogRequestLogging          ← Accurate status codes (after exception handling)
  HTTPS Redirection
  Authentication                 ← Must be before Authorization
  Authorization                  ← Requires Authentication
  Controllers                    ← Business logic entry point
◄── Response
```

**Why CorrelationId must be first:**
If SecurityHeaders runs first, CorrelationId is not in LogContext for that middleware's logs.
More importantly: if Exception handler runs before CorrelationId, error responses won't have the correlation ID.

**Why Exception handler wraps Serilog logging:**
Serilog middleware logs the *final* status code. If ExceptionHandlingMiddleware changes a 500 to a 422, Serilog logs 422. If their order were reversed, Serilog would log 500 for all exceptions.

---

## 12. Logging Architecture

```
Application Code
    │ ILogger<T>.LogInformation(...)
    ▼
Serilog (Logging facade)
    │
    ├──► Console Sink (development: colorized, structured)
    │
    ├──► Application Insights Sink (production: all logs + custom properties)
    │      └─► Azure Monitor / Log Analytics Workspace
    │            └─► Kusto queries, alerts, dashboards
    │
    └──► Seq / Splunk / ELK (optional: enterprise log aggregation)

Log Enrichers (added to every log entry):
  - CorrelationId (from LogContext, set by CorrelationIdMiddleware)
  - MachineName (from environment — identifies which instance in multi-instance deployment)
  - ThreadId (for debugging concurrency issues)
  - Environment (Development/Staging/Production)
```

**Structured logging philosophy:**
```csharp
// ❌ Junior: string interpolation — unsearchable
logger.LogInformation($"Ticket {ticketId} created by user {userId}");

// ✅ Enterprise: structured properties — queryable in Application Insights
logger.LogInformation("Ticket {TicketId} created by user {UserId}", ticketId, userId);
```

In Application Insights / Kusto:
```kql
traces
| where customDimensions.TicketId == "abc-123"
| where customDimensions.CorrelationId == "xyz-456"
| project timestamp, message, severityLevel
```

**Security — what NOT to log:**
- Passwords, tokens, secrets
- Full JWT payload (contains claims/PII)
- Credit card numbers, SSNs
- Full request bodies for authentication endpoints
- Customer PII in Info/Debug level (Error/Warning only, with masking)

---

## 13. Exception Handling Strategy

```
Domain Layer:     DomainException (business rule violated)
                  ├── TicketNotFoundException
                  └── InvalidTicketStateException

Application Layer: NotFoundException (resource not found across aggregates)
                   ValidationException (FluentValidation failures)
                   ForbiddenAccessException (insufficient permissions)

Infrastructure:   Transient exceptions wrapped by Polly (retry/circuit break)
                  TimeoutException, HttpRequestException → retried internally

API Layer:        ExceptionHandlingMiddleware catches ALL
                  Maps to RFC 7807 ProblemDetails
                  └── ValidationException → 422 + error dictionary
                  └── NotFoundException → 404
                  └── ForbiddenAccessException → 403
                  └── DomainException → 400 + errorCode
                  └── Everything else → 500 (safe message only)
```

**The key principle**: Inner layers throw meaningful exceptions; outer layers translate them to HTTP semantics. The domain doesn't know about HTTP. The API doesn't know about FetchXML.

---

## 14. Configuration Strategy

```
Priority (highest to lowest):
  1. Azure Key Vault (production secrets)      ← via Managed Identity, no credentials needed
  2. Environment Variables                     ← Docker, App Service App Settings
  3. User Secrets (dotnet user-secrets)        ← local developer overrides
  4. appsettings.Development.json             ← development defaults
  5. appsettings.json                          ← base defaults (placeholder values)
```

**Production secret flow:**
```
App Service Managed Identity
    │ (no password, Azure-managed certificate rotation)
    ▼
Azure Key Vault
    │ RBAC: Key Vault Secrets User role
    ▼
Secret references in App Settings:
  @Microsoft.KeyVault(SecretUri=https://vault.../secrets/dataverse-client-secret/)
    │
    ▼
ASPNETCORE_DATAVERSE__CLIENTSECRET = [actual secret value, injected by App Service]
```

**Developer secret flow:**
```bash
cd src/4-API/EnterpriseTicketing.API
dotnet user-secrets set "Dataverse:ClientSecret" "your-secret-here"
dotnet user-secrets set "ServiceBus:ConnectionString" "your-connection-here"
```

---

## 15. Security Architecture

### Defense in Depth

```
Layer 1: Azure AD Token validation (JWT signature, audience, issuer, expiry)
Layer 2: Authorization policies (role-based: TicketAgent, TicketManager, Administrator)
Layer 3: Resource-level authorization (can this user access this specific ticket?)
Layer 4: Input validation (FluentValidation + model binding)
Layer 5: Dataverse security roles (row-level and column-level security in Power Platform)
Layer 6: Network security (App Service IP restrictions, VNET integration)
Layer 7: Managed Identity (no passwords for Azure service authentication)
Layer 8: Key Vault (secrets never in configuration files or source control)
Layer 9: HTTPS only (TLS 1.2 minimum)
Layer 10: Security headers (XSS, clickjacking, content sniffing protection)
```

---

## 16. Scalability Considerations

### Horizontal scaling (scale out):

| Component | Scaling strategy |
|-----------|-----------------|
| ASP.NET Core API | App Service auto-scale (CPU > 70%, request queue > 10) |
| Background Service | Runs on each instance — competing consumers pattern on Service Bus |
| Azure Functions | Event-driven auto-scale based on Service Bus queue depth |
| Dataverse SDK | ServiceClient is connection-pooled, thread-safe → scales naturally |

### Bottlenecks to watch:

1. **Dataverse API throttling**: 6,000 requests/5min per user. Implement caching for read-heavy workloads. Use Service Principal with generous limits.

2. **Service Bus throughput**: Standard tier: 256 KB/msg, 10M operations/month. Premium: unlimited, dedicated capacity.

3. **Token acquisition overhead**: DataverseTokenProvider caches tokens — eliminates per-request AAD calls.

4. **Large result sets**: Never return unbounded queries. Always paginate with `top` + `skip` or paging cookies.

---

## 17. Production Support

### Health Checks

| Endpoint | Purpose | Used by |
|----------|---------|---------|
| `/health/live` | "Is the process alive?" — CPU/memory only | Load balancer, k8s liveness probe |
| `/health/ready` | "Can the process serve traffic?" — checks Dataverse, Service Bus | k8s readiness probe, deployment validation |
| `/health` | Full dependency check | Operations monitoring |

### Key Alerts to Configure

```
Application Insights Alerts:
  - Availability < 99.9% (5-min window) → PagerDuty
  - Response time P95 > 2000ms (15-min window) → Slack ops channel
  - Exception rate > 5/min (5-min window) → PagerDuty
  - Dataverse circuit breaker OPEN → immediate PagerDuty
  - Service Bus dead letter count > 10 → Slack + email
  - Failed requests > 1% (5-min window) → Slack ops channel
```

### Dashboard (Azure Monitor Workbook)

- Request volume (req/min by endpoint)
- P50/P95/P99 response times
- Error rate (4xx and 5xx separately)
- Active ticket count by status
- Service Bus queue depths
- Dataverse API call latency
- Active circuit breakers

---

## 18. Tradeoff Analysis

### SDK vs Web API
| Factor | SDK | Web API |
|--------|-----|---------|
| Performance | ✅ Connection pooling, binary protocol | ⚠️ HTTP overhead |
| Type safety | ✅ Strongly typed (early binding) | ⚠️ JSON + OData strings |
| Cross-language | ❌ .NET only | ✅ Any language |
| Query power | ✅ FetchXML (unlimited joins) | ⚠️ OData has limits |
| Package size | ⚠️ ~60MB | ✅ Minimal |
| Version coupling | ⚠️ Must align SDK version | ✅ API version in URL |

**Decision**: SDK for main API (.NET server context). Web API for microservices and scripts.

### Sync vs Async (Service Bus)
| Factor | Sync (direct call) | Async (Service Bus) |
|--------|--------------------|---------------------|
| Latency | ✅ Immediate | ⚠️ Eventual |
| Reliability | ❌ Caller fails = action lost | ✅ Guaranteed delivery |
| Coupling | ❌ Tight (both must be up) | ✅ Loose |
| Complexity | ✅ Simple | ⚠️ Operational overhead |

**Decision**: Sync for read queries. Async for domain events and integrations.

### Monolith vs Microservices (current state)
We start monolithic for speed of delivery and evolve to microservices as team/scale demands grow. See Milestone 5 roadmap.

---

## 19. Enterprise vs Junior Patterns

### What a junior developer would do differently:

```csharp
// ❌ Junior: Logic in controller
[HttpPost]
public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request) {
    var serviceClient = new ServiceClient(connectionString); // Created per request!
    var entity = new Entity("new_ticket");
    entity["new_title"] = request.Title;
    // ... 50 lines of mapping ...
    var id = await serviceClient.CreateAsync(entity);
    await emailService.SendEmail(customerEmail, "Ticket created");  // Coupled!
    return Ok(id);
}
```

```csharp
// ✅ Enterprise: Controller dispatches to handler via MediatR
[HttpPost]
public async Task<ActionResult> CreateTicket(
    [FromBody] CreateTicketRequest request, CancellationToken ct)
{
    var ticketId = await _sender.Send(
        new CreateTicketCommand { /* ... */ }, ct);
    return CreatedAtAction(nameof(GetTicket), new { id = ticketId }, null);
}
```

### What a senior architect considers:

1. **Blast radius** — If this component fails, what breaks? (Service Bus = isolated failure)
2. **Deployment independence** — Can I deploy this without touching other services?
3. **Observable by default** — Will ops know before customers?
4. **Secure by default** — What's the least privileged way to do this?
5. **Reversible decisions** — Can I undo this in 6 months?
6. **Team cognitive load** — Will a junior understand this in 2 years?

---

## 20. Evolution Roadmap

### Current State: Modular Monolith (Milestones 1-4)
- Single deployable API
- Clean internal architecture
- Async via Service Bus
- Functions for event processing
- **Good for: teams < 10, unclear domain boundaries**

### Phase 2: Service Extraction (Milestone 5)
Trigger: specific services have significantly different scaling needs or team ownership:
- Extract NotificationService (notification-specific logic, separate deployment)
- Extract ReportingService (read-only, high-query CQRS read models)
- **Good for: teams 10-50, clear domain ownership**

### Phase 3: Full Microservices
Triggers: independent deployment frequency > 1/day, team > 50, regulatory isolation needed:
- TicketService, CustomerService, NotificationService, IntegrationService, ReportingService
- Azure Container Apps (Kubernetes-lite with KEDA scaling)
- Azure API Management as unified gateway
- Distributed tracing: W3C TraceContext, Application Insights cross-service maps
- **Good for: large orgs with autonomous teams**

### Technology evolution:
- **Storage**: Dataverse → Add read replicas (Synapse Link) for reporting
- **Caching**: Add Redis for token cache, idempotency store, hot data
- **Search**: Azure Cognitive Search for full-text ticket search
- **AI**: Azure OpenAI for ticket classification, routing, summarization
- **Analytics**: Azure Synapse + Power BI for executive dashboards
