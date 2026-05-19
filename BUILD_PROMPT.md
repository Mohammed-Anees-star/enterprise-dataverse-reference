# Enterprise Dataverse Reference Implementation — Build Specification

## Mission
Build a production-grade, enterprise-level reference implementation for a **Ticket Management System** using:
- ASP.NET Core Web API (.NET 10)
- Microsoft Dataverse (SDK + Web API)
- Power Platform Model-driven App (design/configuration files)
- Azure cloud services
- Enterprise Clean Architecture (5 milestones)

This is NOT a tutorial/demo. This is an enterprise reference implementation that looks like it was built by senior architects and principal engineers at a Fortune 500 company.

---

## Complete Repository Structure to Build

```
enterprise-dataverse/
├── README.md                          ← Enterprise README
├── ARCHITECTURE.md                    ← Full architecture documentation
├── .env.example                       ← Environment variable template
├── .gitignore
├── docker-compose.yml
├── docker-compose.override.yml
│
├── src/
│   ├── EnterpriseTicketing.sln        ← Solution file
│   │
│   ├── 1-Domain/
│   │   └── EnterpriseTicketing.Domain/
│   │       ├── EnterpriseTicketing.Domain.csproj
│   │       ├── Entities/
│   │       │   ├── Ticket.cs
│   │       │   ├── TicketComment.cs
│   │       │   ├── TicketAttachment.cs
│   │       │   └── Customer.cs
│   │       ├── Enums/
│   │       │   ├── TicketStatus.cs
│   │       │   ├── TicketPriority.cs
│   │       │   └── TicketCategory.cs
│   │       ├── ValueObjects/
│   │       │   ├── TicketNumber.cs
│   │       │   └── EmailAddress.cs
│   │       ├── Events/
│   │       │   ├── IDomainEvent.cs
│   │       │   ├── TicketCreatedEvent.cs
│   │       │   ├── TicketStatusChangedEvent.cs
│   │       │   └── TicketEscalatedEvent.cs
│   │       ├── Exceptions/
│   │       │   ├── DomainException.cs
│   │       │   ├── TicketNotFoundException.cs
│   │       │   └── InvalidTicketStateException.cs
│   │       └── Interfaces/
│   │           ├── ITicketRepository.cs
│   │           ├── ICustomerRepository.cs
│   │           └── IUnitOfWork.cs
│   │
│   ├── 2-Application/
│   │   └── EnterpriseTicketing.Application/
│   │       ├── EnterpriseTicketing.Application.csproj
│   │       ├── Common/
│   │       │   ├── Behaviors/
│   │       │   │   ├── LoggingBehavior.cs
│   │       │   │   ├── ValidationBehavior.cs
│   │       │   │   ├── PerformanceBehavior.cs
│   │       │   │   └── UnhandledExceptionBehavior.cs
│   │       │   ├── Exceptions/
│   │       │   │   ├── ApplicationException.cs
│   │       │   │   ├── ValidationException.cs
│   │       │   │   ├── ForbiddenAccessException.cs
│   │       │   │   └── NotFoundException.cs
│   │       │   ├── Interfaces/
│   │       │   │   ├── IDataverseService.cs
│   │       │   │   ├── IDataverseWebApiService.cs
│   │       │   │   ├── IEventBus.cs
│   │       │   │   ├── ICurrentUserService.cs
│   │       │   │   └── IDateTimeService.cs
│   │       │   └── Models/
│   │       │       ├── PaginatedList.cs
│   │       │       └── Result.cs
│   │       ├── Tickets/
│   │       │   ├── Commands/
│   │       │   │   ├── CreateTicket/
│   │       │   │   │   ├── CreateTicketCommand.cs
│   │       │   │   │   ├── CreateTicketCommandHandler.cs
│   │       │   │   │   └── CreateTicketCommandValidator.cs
│   │       │   │   ├── UpdateTicket/
│   │       │   │   │   ├── UpdateTicketCommand.cs
│   │       │   │   │   ├── UpdateTicketCommandHandler.cs
│   │       │   │   │   └── UpdateTicketCommandValidator.cs
│   │       │   │   ├── CloseTicket/
│   │       │   │   │   ├── CloseTicketCommand.cs
│   │       │   │   │   ├── CloseTicketCommandHandler.cs
│   │       │   │   │   └── CloseTicketCommandValidator.cs
│   │       │   │   └── EscalateTicket/
│   │       │   │       ├── EscalateTicketCommand.cs
│   │       │   │       ├── EscalateTicketCommandHandler.cs
│   │       │   │       └── EscalateTicketCommandValidator.cs
│   │       │   ├── Queries/
│   │       │   │   ├── GetTicketById/
│   │       │   │   │   ├── GetTicketByIdQuery.cs
│   │       │   │   │   └── GetTicketByIdQueryHandler.cs
│   │       │   │   ├── GetTickets/
│   │       │   │   │   ├── GetTicketsQuery.cs
│   │       │   │   │   ├── GetTicketsQueryHandler.cs
│   │       │   │   │   └── TicketDto.cs
│   │       │   │   └── GetTicketsByCustomer/
│   │       │   │       ├── GetTicketsByCustomerQuery.cs
│   │       │   │       └── GetTicketsByCustomerQueryHandler.cs
│   │       │   └── EventHandlers/
│   │       │       ├── TicketCreatedEventHandler.cs
│   │       │       └── TicketStatusChangedEventHandler.cs
│   │       └── DependencyInjection.cs
│   │
│   ├── 3-Infrastructure/
│   │   └── EnterpriseTicketing.Infrastructure/
│   │       ├── EnterpriseTicketing.Infrastructure.csproj
│   │       ├── Dataverse/
│   │       │   ├── DataverseService.cs           ← SDK-based
│   │       │   ├── DataverseWebApiService.cs     ← Web API-based
│   │       │   ├── Repositories/
│   │       │   │   ├── TicketRepository.cs
│   │       │   │   └── CustomerRepository.cs
│   │       │   ├── Mapping/
│   │       │   │   ├── TicketMapper.cs
│   │       │   │   └── CustomerMapper.cs
│   │       │   └── Configuration/
│   │       │       └── DataverseConfiguration.cs
│   │       ├── Messaging/
│   │       │   ├── ServiceBusEventBus.cs
│   │       │   ├── ServiceBusConsumer.cs
│   │       │   └── Configuration/
│   │       │       └── ServiceBusConfiguration.cs
│   │       ├── Security/
│   │       │   ├── DataverseTokenProvider.cs
│   │       │   └── CurrentUserService.cs
│   │       ├── Http/
│   │       │   ├── DataverseHttpClientHandler.cs
│   │       │   └── PolicyDefinitions.cs
│   │       ├── BackgroundServices/
│   │       │   └── TicketProcessingBackgroundService.cs
│   │       └── DependencyInjection.cs
│   │
│   ├── 4-API/
│   │   └── EnterpriseTicketing.API/
│   │       ├── EnterpriseTicketing.API.csproj
│   │       ├── Program.cs
│   │       ├── Controllers/
│   │       │   ├── v1/
│   │       │   │   ├── TicketsController.cs
│   │       │   │   └── CustomersController.cs
│   │       │   └── v2/
│   │       │       └── TicketsController.cs
│   │       ├── Middleware/
│   │       │   ├── CorrelationIdMiddleware.cs
│   │       │   ├── ExceptionHandlingMiddleware.cs
│   │       │   ├── RequestLoggingMiddleware.cs
│   │       │   └── SecurityHeadersMiddleware.cs
│   │       ├── Filters/
│   │       │   └── ApiExceptionFilter.cs
│   │       ├── Models/
│   │       │   ├── Requests/
│   │       │   │   ├── CreateTicketRequest.cs
│   │       │   │   └── UpdateTicketRequest.cs
│   │       │   └── Responses/
│   │       │       ├── TicketResponse.cs
│   │       │       └── PaginatedResponse.cs
│   │       ├── Configuration/
│   │       │   ├── SwaggerConfiguration.cs
│   │       │   ├── AuthenticationConfiguration.cs
│   │       │   └── HealthCheckConfiguration.cs
│   │       ├── appsettings.json
│   │       ├── appsettings.Development.json
│   │       └── Dockerfile
│   │
│   └── 5-Functions/
│       └── EnterpriseTicketing.Functions/
│           ├── EnterpriseTicketing.Functions.csproj
│           ├── Functions/
│           │   ├── TicketCreatedFunction.cs
│           │   ├── TicketNotificationFunction.cs
│           │   └── DataverseWebhookFunction.cs
│           ├── host.json
│           ├── local.settings.json.example
│           └── Dockerfile
│
├── tests/
│   ├── EnterpriseTicketing.Domain.Tests/
│   │   ├── EnterpriseTicketing.Domain.Tests.csproj
│   │   └── Entities/
│   │       └── TicketTests.cs
│   ├── EnterpriseTicketing.Application.Tests/
│   │   ├── EnterpriseTicketing.Application.Tests.csproj
│   │   └── Tickets/
│   │       ├── Commands/
│   │       │   └── CreateTicketCommandHandlerTests.cs
│   │       └── Queries/
│   │           └── GetTicketByIdQueryHandlerTests.cs
│   └── EnterpriseTicketing.API.IntegrationTests/
│       ├── EnterpriseTicketing.API.IntegrationTests.csproj
│       └── Controllers/
│           └── TicketsControllerTests.cs
│
├── power-platform/
│   ├── README.md
│   ├── dataverse-tables/
│   │   ├── tickets-table-definition.json
│   │   ├── customers-table-definition.json
│   │   └── ticketcomments-table-definition.json
│   ├── model-driven-app/
│   │   ├── sitemap.xml
│   │   ├── app-definition.json
│   │   └── README.md
│   ├── security-roles/
│   │   └── ticket-manager-role.json
│   └── power-automate/
│       ├── ticket-created-flow.json
│       └── README.md
│
├── infrastructure/
│   ├── azure/
│   │   ├── main.bicep
│   │   ├── app-service.bicep
│   │   ├── service-bus.bicep
│   │   ├── key-vault.bicep
│   │   ├── app-insights.bicep
│   │   └── README.md
│   └── scripts/
│       ├── setup-local-dev.sh
│       └── deploy-azure.sh
│
├── docs/
│   ├── architecture/
│   │   ├── 01-overview.md
│   │   ├── 02-clean-architecture.md
│   │   ├── 03-request-flow.md
│   │   ├── 04-authentication-flow.md
│   │   ├── 05-dataverse-sdk-flow.md
│   │   ├── 06-dataverse-webapi-flow.md
│   │   ├── 07-power-platform-integration.md
│   │   ├── 08-service-bus-flow.md
│   │   ├── 09-azure-functions-flow.md
│   │   ├── 10-event-driven-architecture.md
│   │   ├── 11-dependency-injection.md
│   │   ├── 12-middleware-pipeline.md
│   │   ├── 13-logging-architecture.md
│   │   ├── 14-exception-handling.md
│   │   ├── 15-configuration-strategy.md
│   │   ├── 16-security-architecture.md
│   │   ├── 17-scalability.md
│   │   ├── 18-production-support.md
│   │   ├── 19-tradeoff-analysis.md
│   │   └── 20-evolution-roadmap.md
│   ├── setup/
│   │   ├── local-development.md
│   │   ├── dataverse-setup.md
│   │   └── azure-setup.md
│   └── api/
│       ├── swagger-guide.md
│       └── postman-collection.json
│
└── .github/
    └── workflows/
        ├── ci.yml
        └── cd.yml
```

---

## MILESTONE 1 — Core Enterprise Foundation

### Domain Layer (EnterpriseTicketing.Domain)

**Ticket.cs** — Rich domain entity with business logic:
```
- Properties: Id (Guid), TicketNumber (ValueObject), Title, Description, Status (enum), Priority (enum), Category (enum), CustomerId, AssignedToUserId, CreatedAt, UpdatedAt, ResolvedAt, ClosedAt, EscalationCount
- Methods: Create(), UpdateDetails(), ChangeStatus(), Escalate(), Close(), Resolve()
- Domain events list: List<IDomainEvent>
- Business rules enforced in domain (e.g., cannot close a ticket that is already closed, cannot escalate a resolved ticket)
```

**TicketNumber.cs** — Value object:
```
- Format: TKT-{YYYY}-{000000}
- Auto-generated from year and sequential number
- Implicit/explicit conversions
- Equality by value
```

**Domain Events** — Each significant state change raises a domain event:
- TicketCreatedEvent (TicketId, TicketNumber, CustomerId, Priority, CreatedAt)
- TicketStatusChangedEvent (TicketId, OldStatus, NewStatus, ChangedBy, ChangedAt)
- TicketEscalatedEvent (TicketId, EscalationLevel, EscalatedAt, Reason)

**Repository Interfaces** — Clean abstractions:
```csharp
public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByTicketNumberAsync(string ticketNumber, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Ticket>> GetPagedAsync(TicketFilter filter, PaginationOptions pagination, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ticket>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
```

### Application Layer (EnterpriseTicketing.Application)

Use **MediatR** for CQRS pattern. Use **FluentValidation** for all validators.

**CreateTicketCommand:**
```csharp
public record CreateTicketCommand : IRequest<Result<Guid>>
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TicketPriority Priority { get; init; }
    public TicketCategory Category { get; init; }
    public Guid CustomerId { get; init; }
    public string? AssignedToUserId { get; init; }
}
```

**CreateTicketCommandHandler:**
- Validates customer exists
- Creates Ticket domain entity via factory method
- Persists via ITicketRepository
- Publishes domain events via IEventBus
- Returns Result<Guid> with the new ticket ID

**MediatR Pipeline Behaviors:**
1. UnhandledExceptionBehavior — logs unhandled exceptions
2. ValidationBehavior — runs FluentValidation, throws ValidationException
3. PerformanceBehavior — logs slow requests (>500ms warning, >2000ms critical)
4. LoggingBehavior — structured request/response logging

**Application Interfaces:**
```csharp
public interface IDataverseService
{
    Task<Guid> CreateEntityAsync(string entityLogicalName, Dictionary<string, object> attributes, CancellationToken ct = default);
    Task<T?> GetEntityAsync<T>(string entityLogicalName, Guid id, string[] columns, CancellationToken ct = default) where T : class;
    Task UpdateEntityAsync(string entityLogicalName, Guid id, Dictionary<string, object> attributes, CancellationToken ct = default);
    Task DeleteEntityAsync(string entityLogicalName, Guid id, CancellationToken ct = default);
    Task<PagedResult<T>> QueryEntitiesAsync<T>(string entityLogicalName, string fetchXml, CancellationToken ct = default) where T : class;
}

public interface IDataverseWebApiService
{
    Task<T?> GetAsync<T>(string odataPath, string? selectColumns = null, CancellationToken ct = default) where T : class;
    Task<ODataCollection<T>> QueryAsync<T>(string entitySetName, ODataQueryOptions options, CancellationToken ct = default) where T : class;
    Task<Guid> CreateAsync(string entitySetName, object payload, CancellationToken ct = default);
    Task UpdateAsync(string entitySetName, Guid id, object payload, CancellationToken ct = default);
    Task DeleteAsync(string entitySetName, Guid id, CancellationToken ct = default);
}

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
    Task SubscribeAsync<T>(Func<T, Task> handler, CancellationToken ct = default) where T : class;
}
```

### Infrastructure Layer (EnterpriseTicketing.Infrastructure)

**DataverseService.cs** — SDK-based implementation:
```csharp
public sealed class DataverseService : IDataverseService
{
    private readonly ServiceClient _serviceClient;
    private readonly ILogger<DataverseService> _logger;
    private readonly IAsyncPolicy _retryPolicy;
    
    // Constructor injects ServiceClient, logger, Polly policy
    // ServiceClient created from IOptions<DataverseConfiguration>
    
    // CreateEntityAsync: Entity entity = new(entityLogicalName); set attributes; _serviceClient.CreateAsync()
    // GetEntityAsync: ColumnSet columns; Entity result = await _serviceClient.RetrieveAsync(); map to T
    // UpdateEntityAsync: Entity entity; _serviceClient.UpdateAsync()
    // DeleteEntityAsync: _serviceClient.DeleteAsync()
    // QueryEntitiesAsync: FetchExpression fetchExpression; EntityCollection results = await _serviceClient.RetrieveMultipleAsync()
}
```

**DataverseWebApiService.cs** — HttpClient-based Web API:
```csharp
public sealed class DataverseWebApiService : IDataverseWebApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DataverseWebApiService> _logger;
    // Token handled by DataverseHttpClientHandler (DelegatingHandler)
    
    // GetAsync: GET /api/data/v9.2/{path}?$select={columns}
    // QueryAsync: GET /api/data/v9.2/{entityset}?$filter=...&$orderby=...&$top=...&$skip=...
    // CreateAsync: POST /api/data/v9.2/{entityset}; return OData-EntityId header as Guid
    // UpdateAsync: PATCH /api/data/v9.2/{entityset}({id})
    // DeleteAsync: DELETE /api/data/v9.2/{entityset}({id})
}
```

**DataverseHttpClientHandler.cs** — Token injection DelegatingHandler:
```csharp
public sealed class DataverseHttpClientHandler : DelegatingHandler
{
    // Gets token from IDataverseTokenProvider
    // Adds: Authorization: Bearer {token}
    // Adds: OData-MaxVersion: 4.0
    // Adds: OData-Version: 4.0
    // Adds: Accept: application/json
    // Adds: Prefer: odata.include-annotations="*"
}
```

**DataverseTokenProvider.cs:**
```csharp
public sealed class DataverseTokenProvider : IDataverseTokenProvider
{
    // Uses MSAL ConfidentialClientApplication
    // Caches token in memory (IMemoryCache) with expiry buffer (5 min before actual expiry)
    // Acquires using client credentials flow
    // Scope: {dataverseUrl}/.default
}
```

**PolicyDefinitions.cs** — Polly resilience:
```csharp
public static class PolicyDefinitions
{
    // DataverseRetryPolicy: Retry 3 times with exponential backoff + jitter
    //   Retry on: HttpRequestException, TaskCanceledException, 429, 500, 502, 503, 504
    //   Jitter: Random(0-1000ms) added to each wait
    
    // DataverseCircuitBreakerPolicy: Break after 5 failures in 30s, break for 60s
    
    // DataverseWrapPolicy: Wrap(CircuitBreaker, Retry)
    
    // ServiceBusRetryPolicy: Retry 5 times with fixed 2s backoff
}
```

**TicketRepository.cs** — Dataverse-backed repository:
```csharp
public sealed class TicketRepository : ITicketRepository
{
    // Uses IDataverseService for read/write
    // Table: new_tickets (Dataverse logical name)
    // Maps Ticket domain entity to Dataverse attribute dictionary
    // Maps Dataverse Entity to Ticket domain entity
    // Implements all ITicketRepository methods
    // Uses FetchXML for complex queries
    
    // Column mapping:
    // new_ticketid → Id
    // new_ticketnumber → TicketNumber
    // new_title → Title
    // new_description → Description
    // new_status → Status (OptionSet)
    // new_priority → Priority (OptionSet)
    // new_category → Category (OptionSet)
    // new_customerid → CustomerId (EntityReference)
    // new_assignedtouserid → AssignedToUserId
    // createdon → CreatedAt
    // modifiedon → UpdatedAt
}
```

### API Layer (EnterpriseTicketing.API)

**Program.cs** — Enterprise bootstrap:
```csharp
// Use WebApplication.CreateBuilder(args)
// Configure Serilog from configuration (ReadFrom.Configuration)
// AddApplicationInsightsTelemetry
// AddAuthentication(JwtBearerDefaults) with Azure AD / Entra ID config
// AddAuthorization with policies
// AddApiVersioning + AddVersionedApiExplorer
// AddSwaggerGen with OAuth2 security scheme + versioning support
// AddHealthChecks with DataverseHealthCheck, ServiceBusHealthCheck
// AddControllers with custom JSON options
// AddFluentValidation
// AddMediatR (Application assembly)
// AddApplication() (from Application DI extension)
// AddInfrastructure() (from Infrastructure DI extension)
// AddCorrelationId
// UseSerilogRequestLogging
// UseMiddleware<CorrelationIdMiddleware>
// UseMiddleware<SecurityHeadersMiddleware>
// UseMiddleware<ExceptionHandlingMiddleware>
// UseAuthentication + UseAuthorization
// MapControllers
// MapHealthChecks("/health", "/health/ready", "/health/live")
// MapSwagger
```

**CorrelationIdMiddleware.cs:**
```csharp
// Reads X-Correlation-ID header or generates new Guid
// Stores in HttpContext.Items["CorrelationId"]
// Adds to response headers
// Adds to Serilog LogContext as "CorrelationId"
```

**ExceptionHandlingMiddleware.cs:**
```csharp
// Catches all unhandled exceptions
// Maps to appropriate HTTP status codes:
//   ValidationException → 422 with error details
//   NotFoundException → 404
//   ForbiddenAccessException → 403
//   DomainException → 400
//   Exception → 500 (with correlation ID, without stack trace in production)
// Returns RFC 7807 ProblemDetails format
// Logs with structured context (CorrelationId, ExceptionType, ExceptionMessage)
```

**SecurityHeadersMiddleware.cs:**
```csharp
// Adds: X-Content-Type-Options: nosniff
// Adds: X-Frame-Options: DENY
// Adds: X-XSS-Protection: 1; mode=block
// Adds: Referrer-Policy: strict-origin-when-cross-origin
// Adds: Content-Security-Policy: default-src 'self'
// Removes: Server header
```

**TicketsController.cs (v1):**
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class TicketsController : ControllerBase
{
    // Constructor: IMediator, ILogger<TicketsController>
    
    [HttpGet]                    // GET /api/v1/tickets?page=1&pageSize=20&status=Open&priority=High
    [HttpGet("{id:guid}")]       // GET /api/v1/tickets/{id}
    [HttpPost]                   // POST /api/v1/tickets
    [HttpPut("{id:guid}")]       // PUT /api/v1/tickets/{id}
    [HttpPost("{id:guid}/close")] // POST /api/v1/tickets/{id}/close
    [HttpPost("{id:guid}/escalate")] // POST /api/v1/tickets/{id}/escalate
    [HttpDelete("{id:guid}")]    // DELETE /api/v1/tickets/{id}
    
    // Each action maps request DTO → command/query → sends via MediatR → maps result → returns appropriate status
    // Use ActionResult<T> return types
    // Decorate with [ProducesResponseType] attributes
    // Use [FromBody], [FromQuery], [FromRoute] explicitly
}
```

**appsettings.json** (PLACEHOLDER VALUES ONLY — never real secrets):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.PowerPlatform": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console", "Args": { "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}" } },
      {
        "Name": "ApplicationInsights",
        "Args": {
          "connectionString": "{ApplicationInsights:ConnectionString}",
          "telemetryConverter": "Serilog.Sinks.ApplicationInsights.TelemetryConverters.TraceTelemetryConverter, Serilog.Sinks.ApplicationInsights"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId", "WithCorrelationId"]
  },
  "AzureAd": {
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "Instance": "https://login.microsoftonline.com/",
    "Audience": "YOUR_API_AUDIENCE"
  },
  "Dataverse": {
    "Url": "https://YOUR_ORG.crm.dynamics.com",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_DATAVERSE_APP_CLIENT_ID",
    "ClientSecret": "PLACEHOLDER_USE_KEY_VAULT_OR_ENV",
    "MaxRetryCount": 3,
    "TimeoutSeconds": 30
  },
  "ServiceBus": {
    "ConnectionString": "PLACEHOLDER_USE_KEY_VAULT_OR_ENV",
    "TicketEventsQueueName": "ticket-events",
    "NotificationsQueueName": "ticket-notifications",
    "DeadLetterQueueSuffix": "/$deadletterqueue"
  },
  "ApplicationInsights": {
    "ConnectionString": "PLACEHOLDER_USE_KEY_VAULT_OR_ENV"
  },
  "KeyVault": {
    "VaultUri": "https://YOUR_KEYVAULT.vault.azure.net/"
  },
  "HealthChecks": {
    "DataverseTimeoutSeconds": 10,
    "ServiceBusTimeoutSeconds": 5
  },
  "Api": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "RateLimitPerMinute": 100
  }
}
```

**appsettings.Development.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    },
    "WriteTo": [
      { "Name": "Console", "Args": { "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console" } }
    ]
  },
  "AzureAd": {
    "TenantId": "YOUR_DEV_TENANT_ID",
    "ClientId": "YOUR_DEV_CLIENT_ID"
  },
  "Dataverse": {
    "Url": "https://YOUR_DEV_ORG.crm.dynamics.com"
  }
}
```

**Dockerfile (API):**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/4-API/EnterpriseTicketing.API/EnterpriseTicketing.API.csproj", "4-API/EnterpriseTicketing.API/"]
COPY ["src/3-Infrastructure/EnterpriseTicketing.Infrastructure/EnterpriseTicketing.Infrastructure.csproj", "3-Infrastructure/EnterpriseTicketing.Infrastructure/"]
COPY ["src/2-Application/EnterpriseTicketing.Application/EnterpriseTicketing.Application.csproj", "2-Application/EnterpriseTicketing.Application/"]
COPY ["src/1-Domain/EnterpriseTicketing.Domain/EnterpriseTicketing.Domain.csproj", "1-Domain/EnterpriseTicketing.Domain/"]
RUN dotnet restore "4-API/EnterpriseTicketing.API/EnterpriseTicketing.API.csproj"
COPY src/ .
WORKDIR "/src/4-API/EnterpriseTicketing.API"
RUN dotnet build "EnterpriseTicketing.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EnterpriseTicketing.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER app
ENTRYPOINT ["dotnet", "EnterpriseTicketing.API.dll"]
```

**docker-compose.yml:**
```yaml
version: '3.8'
services:
  enterprise-ticketing-api:
    image: enterprise-ticketing-api:latest
    build:
      context: .
      dockerfile: src/4-API/EnterpriseTicketing.API/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
    env_file:
      - .env
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
```

**.env.example:**
```
# Copy to .env and fill in values for local development
# NEVER commit .env to source control

# Azure AD / Entra ID
AZUREAD__TENANTID=your-tenant-id
AZUREAD__CLIENTID=your-api-client-id

# Dataverse
DATAVERSE__URL=https://yourorg.crm.dynamics.com
DATAVERSE__TENANTID=your-tenant-id
DATAVERSE__CLIENTID=your-app-client-id
DATAVERSE__CLIENTSECRET=your-client-secret

# Azure Service Bus
SERVICEBUS__CONNECTIONSTRING=Endpoint=sb://...

# Application Insights
APPLICATIONINSIGHTS__CONNECTIONSTRING=InstrumentationKey=...

# Azure Key Vault (optional for local dev)
KEYVAULT__VAULTURI=https://yourvault.vault.azure.net/
```

### NuGet Package References

**Domain project:**
- No external dependencies (pure domain model)

**Application project:**
- MediatR 12.x
- FluentValidation.DependencyInjectionExtensions
- Microsoft.Extensions.Logging.Abstractions
- Microsoft.Extensions.DependencyInjection.Abstractions

**Infrastructure project:**
- Microsoft.PowerPlatform.Dataverse.Client
- Microsoft.Identity.Client (MSAL)
- Polly 8.x
- Azure.Messaging.ServiceBus
- Microsoft.Extensions.Http.Polly
- Serilog
- AutoMapper

**API project:**
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.Identity.Web
- Swashbuckle.AspNetCore
- Asp.Versioning.Mvc
- Asp.Versioning.ApiExplorer
- Serilog.AspNetCore
- Serilog.Sinks.ApplicationInsights
- Serilog.Enrichers.CorrelationId
- Microsoft.ApplicationInsights.AspNetCore
- AspNetCore.HealthChecks.AzureServiceBus
- Microsoft.Extensions.Diagnostics.HealthChecks

---

## MILESTONE 2 — Dataverse Web API Integration

Add to Infrastructure:

1. **ODataQueryOptions.cs** — Builder pattern for OData queries:
```csharp
public sealed class ODataQueryOptions
{
    public string? Select { get; private set; }
    public string? Filter { get; private set; }
    public string? OrderBy { get; private set; }
    public int? Top { get; private set; }
    public int? Skip { get; private set; }
    public string? Expand { get; private set; }
    public bool Count { get; private set; }
    
    // Fluent builder methods: WithSelect, WithFilter, WithOrderBy, WithTop, WithSkip, WithExpand, WithCount
    // BuildQueryString() → "?$select=...&$filter=...&$top=..."
}
```

2. **ODataCollection<T>.cs** — Deserializes OData @odata.context, @odata.count, value array

3. **DataverseWebApiService.cs** — Full implementation with HttpClientFactory:
```csharp
// Named HttpClient "DataverseWebApi" registered with IHttpClientFactory
// Base address: {DataverseUrl}/api/data/v9.2/
// Default headers set in registration
// DataverseHttpClientHandler as DelegatingHandler for token injection
// Polly policies attached to named client
// Error handling: maps 400/401/403/404/409/429/5xx to typed exceptions
// Serialization: System.Text.Json with camelCase + enum string conversion
```

4. **TicketWebApiRepository.cs** — Alternative repository using Web API:
```csharp
// Demonstrates: when to use Web API vs SDK
// Complex OData query example with $filter, $select, $expand, $orderby
// Batch operation example using $batch endpoint
// Change tracking example using @odata.etag
```

5. **Architecture Decision Record** in docs/architecture:
```
ADR-001: Dataverse SDK vs Web API
- SDK: Synchronous operations, server-side plugins, complex queries with FetchXML, type safety
- Web API: Microservice integration, language-agnostic, OData standards, horizontal scaling
- Decision: Use SDK as primary, Web API for specific scenarios requiring HTTP-native patterns
```

---

## MILESTONE 3 — Azure Service Bus Integration

**ServiceBusEventBus.cs:**
```csharp
public sealed class ServiceBusEventBus : IEventBus, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusEventBus> _logger;
    
    // PublishAsync<T>: 
    //   Serialize event to JSON
    //   Create ServiceBusMessage with:
    //     - MessageId = Guid.NewGuid() (idempotency)
    //     - CorrelationId from ICorrelationIdProvider
    //     - Subject = typeof(T).Name
    //     - ContentType = "application/json"
    //     - Body = BinaryData.FromString(json)
    //     - ApplicationProperties["EventType"] = typeof(T).FullName
    //     - ApplicationProperties["Version"] = "1.0"
    //   Send with retry policy
    //   Log with EventType, MessageId, CorrelationId
}
```

**ServiceBusConsumer.cs** — Background service:
```csharp
public sealed class ServiceBusConsumer : BackgroundService
{
    // Creates ServiceBusProcessor for each queue
    // ProcessMessageAsync handler:
    //   Deserialize message body
    //   Route to appropriate handler based on Subject/EventType property
    //   Complete message on success
    //   Abandon with DeadLetter after MaxDeliveryCount exceeded
    //   Log all processing with MessageId, CorrelationId
    // ProcessErrorAsync handler:
    //   Log error with full context
    //   Trigger alert if needed
    
    // Idempotency: check IIdempotencyStore before processing
    //   IIdempotencyStore backed by IMemoryCache (local) or Redis (distributed)
}
```

**Dead Letter Queue Strategy:**
```csharp
// DeadLetterProcessor.cs - separate background service
// Reads from DLQ on a scheduled basis (every 5 minutes)
// Logs dead letter reasons to Application Insights custom events
// Optionally requeues messages that failed due to transient errors
// Sends alerts for poison messages (cannot be processed after retry)
```

**TicketProcessingBackgroundService.cs:**
```csharp
public sealed class TicketProcessingBackgroundService : BackgroundService
{
    // Processes TicketCreatedEvent messages from Service Bus
    // For each message:
    //   - Looks up ticket from Dataverse
    //   - Sends welcome notification to customer
    //   - Updates ticket status to "InProgress" if auto-assignment is enabled
    //   - Records processing in audit log
    // Handles transient failures with local retry
    // Dead-letters after 3 consecutive failures
}
```

---

## MILESTONE 4 — Azure Functions

**EnterpriseTicketing.Functions project (.NET 10 Isolated Worker):**

**TicketCreatedFunction.cs:**
```csharp
public class TicketCreatedFunction
{
    [Function("TicketCreatedProcessor")]
    public async Task Run(
        [ServiceBusTrigger("ticket-events", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext executionContext)
    {
        // Extract correlation ID from message ApplicationProperties
        // Deserialize TicketCreatedEvent
        // Process: update CRM, send notifications, trigger downstream integrations
        // Complete or dead-letter based on result
        // Log with Application Insights distributed tracing
    }
}
```

**DataverseWebhookFunction.cs:**
```csharp
public class DataverseWebhookFunction
{
    [Function("DataverseWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dataverse/webhook")] HttpRequest req,
        FunctionContext executionContext)
    {
        // Receives Dataverse plugin/webhook callbacks
        // Validates request authenticity (shared secret or certificate)
        // Routes to appropriate handler based on MessageName (Create, Update, Delete)
        // Publishes integration events to Service Bus
        // Returns 200 OK synchronously (Dataverse requires fast response)
    }
}
```

**TicketNotificationFunction.cs:**
```csharp
public class TicketNotificationFunction
{
    [Function("TicketNotificationProcessor")]
    public async Task Run(
        [ServiceBusTrigger("ticket-notifications", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext executionContext)
    {
        // Processes notification events
        // Sends email via SendGrid / Azure Communication Services
        // Updates notification status in Dataverse
        // Handles notification templates
    }
}
```

**host.json:**
```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20
      }
    },
    "logLevel": {
      "default": "Information",
      "Function": "Information"
    }
  },
  "extensions": {
    "serviceBus": {
      "prefetchCount": 0,
      "messageHandlerOptions": {
        "maxConcurrentCalls": 16,
        "autoComplete": false
      }
    }
  }
}
```

---

## MILESTONE 5 — Microservices Evolution

Create a docs/architecture/microservices-evolution directory with:

**microservices-architecture.md** — Comprehensive document explaining:

1. **Service Boundaries** (based on domain contexts):
   - TicketService: Core ticket lifecycle management
   - CustomerService: Customer profiles and history
   - NotificationService: All notification delivery (email, SMS, push)
   - IntegrationService: External system adapters
   - ReportingService: Analytics, dashboards, CQRS read models

2. **Communication Patterns:**
   - Synchronous: REST over HTTPS for query operations, API Gateway pattern
   - Asynchronous: Service Bus topics/subscriptions for domain events
   - Event schema versioning strategy

3. **API Gateway:**
   - Azure API Management (APIM) as enterprise gateway
   - Rate limiting, quotas, caching at gateway level
   - JWT validation at gateway (offload from services)
   - Request/response transformation
   - Backend circuit breakers

4. **Distributed Tracing:**
   - W3C TraceContext header propagation
   - Application Insights distributed tracing
   - Correlation ID threading through all services
   - Azure Monitor workbooks for end-to-end trace visualization

5. **Eventual Consistency:**
   - Outbox pattern for reliable event publishing
   - Saga pattern for distributed transactions
   - Compensation transactions for failures
   - Idempotent message handlers

6. **Service Ownership:**
   - Each service owns its data store
   - No shared database anti-pattern
   - Data replication via events (materialized views per service)

7. **Scaling Strategies:**
   - Each service scales independently
   - Azure Container Apps for serverless container scaling
   - KEDA for Service Bus-triggered scaling
   - Read replicas for reporting service

Also add **microservices-solution-structure.md** showing how the current solution maps to the microservices future state.

---

## Power Platform Files

### power-platform/README.md
Comprehensive guide explaining:
1. Why Model-driven App is chosen (Dataverse-native, less code, RBAC, offline support, advanced forms)
2. When Canvas App is better (custom UI, consumer-facing, complex UX flows)
3. How MDA works with Dataverse tables
4. How ASP.NET Core API and MDA share the same Dataverse tables
5. Integration architecture between API and Power Platform

### power-platform/dataverse-tables/tickets-table-definition.json
JSON definition showing Dataverse table schema:
```json
{
  "TableLogicalName": "new_ticket",
  "DisplayName": "Ticket",
  "PrimaryNameColumn": "new_title",
  "Columns": [
    { "LogicalName": "new_ticketid", "Type": "UniqueIdentifier", "DisplayName": "Ticket ID" },
    { "LogicalName": "new_ticketnumber", "Type": "String", "MaxLength": 20, "DisplayName": "Ticket Number" },
    { "LogicalName": "new_title", "Type": "String", "MaxLength": 200, "DisplayName": "Title", "Required": true },
    { "LogicalName": "new_description", "Type": "Memo", "MaxLength": 2000, "DisplayName": "Description" },
    { "LogicalName": "new_status", "Type": "OptionSet", "DisplayName": "Status",
      "Options": [
        { "Value": 100000000, "Label": "Open" },
        { "Value": 100000001, "Label": "InProgress" },
        { "Value": 100000002, "Label": "Resolved" },
        { "Value": 100000003, "Label": "Closed" },
        { "Value": 100000004, "Label": "Cancelled" }
      ]
    },
    { "LogicalName": "new_priority", "Type": "OptionSet", "DisplayName": "Priority",
      "Options": [
        { "Value": 100000000, "Label": "Low" },
        { "Value": 100000001, "Label": "Medium" },
        { "Value": 100000002, "Label": "High" },
        { "Value": 100000003, "Label": "Critical" }
      ]
    },
    { "LogicalName": "new_category", "Type": "OptionSet", "DisplayName": "Category" },
    { "LogicalName": "new_customerid", "Type": "Lookup", "RelatedTable": "new_customer", "DisplayName": "Customer" },
    { "LogicalName": "new_assignedtouserid", "Type": "String", "DisplayName": "Assigned To User ID" },
    { "LogicalName": "new_resolvedat", "Type": "DateTime", "DisplayName": "Resolved At" },
    { "LogicalName": "new_closedat", "Type": "DateTime", "DisplayName": "Closed At" },
    { "LogicalName": "new_escalationcount", "Type": "Integer", "DisplayName": "Escalation Count" }
  ],
  "Relationships": [
    {
      "Type": "ManyToOne",
      "ReferencedTable": "new_customer",
      "ReferencingColumn": "new_customerid",
      "DisplayName": "Customer"
    },
    {
      "Type": "OneToMany",
      "ReferencedTable": "new_ticketcomment",
      "DisplayName": "Comments"
    }
  ]
}
```

### power-platform/model-driven-app/sitemap.xml
```xml
<?xml version="1.0" encoding="utf-8"?>
<SiteMap>
  <Area Id="TicketManagement" Title="Ticket Management" Icon="Tickets">
    <Group Id="Operations" Title="Operations">
      <SubArea Id="ActiveTickets" Title="Active Tickets" Entity="new_ticket" DefaultDashboard="TicketsDashboard" />
      <SubArea Id="MyTickets" Title="My Tickets" Entity="new_ticket" />
      <SubArea Id="AllTickets" Title="All Tickets" Entity="new_ticket" />
    </Group>
    <Group Id="Customers" Title="Customers">
      <SubArea Id="AllCustomers" Title="Customers" Entity="new_customer" />
    </Group>
    <Group Id="Reports" Title="Reports">
      <SubArea Id="TicketMetrics" Title="Ticket Metrics" Url="/WebResources/new_TicketMetricsDashboard.html" />
    </Group>
  </Area>
</SiteMap>
```

### power-platform/power-automate/ticket-created-flow.json
JSON representation of a Power Automate flow:
- Trigger: When a row is added (new_ticket table)
- Action 1: Get customer details (new_customer table lookup)
- Action 2: Send email notification to customer
- Action 3: Post to Teams channel if Priority = Critical
- Action 4: Create initial comment "Ticket received, processing"
- Condition: If escalation_count > 2, send manager alert

---

## Infrastructure / Azure Bicep

### infrastructure/azure/main.bicep
```bicep
targetScope = 'resourceGroup'

param location string = resourceGroup().location
param environmentName string
param appName string = 'enterprise-ticketing'

module appService 'app-service.bicep' = {
  name: 'appService'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

module serviceBus 'service-bus.bicep' = {
  name: 'serviceBus'
  params: { location: location, environmentName: environmentName }
}

module keyVault 'key-vault.bicep' = {
  name: 'keyVault'
  params: { location: location, environmentName: environmentName }
}

module appInsights 'app-insights.bicep' = {
  name: 'appInsights'
  params: { location: location, environmentName: environmentName }
}
```

### infrastructure/azure/app-service.bicep
Full Bicep for App Service Plan + Web App:
- SKU: P2v3 for production (configurable)
- Managed Identity enabled (SystemAssigned)
- App Settings: APPLICATIONINSIGHTS_CONNECTION_STRING, KeyVaultUri
- Key Vault reference for secrets: @Microsoft.KeyVault(SecretUri=...)
- Always On: true
- HTTP2: true
- Min TLS: 1.2
- HTTPS Only: true
- Health Check Path: /health/live
- Deployment slots: staging

---

## GitHub Actions CI/CD

### .github/workflows/ci.yml
```yaml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore
        run: dotnet restore src/EnterpriseTicketing.sln
      - name: Build
        run: dotnet build src/EnterpriseTicketing.sln --no-restore -c Release
      - name: Test
        run: dotnet test src/EnterpriseTicketing.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage
      - name: Upload coverage
        uses: codecov/codecov-action@v4
        with:
          directory: ./coverage
      - name: Docker build
        run: docker build -f src/4-API/EnterpriseTicketing.API/Dockerfile -t enterprise-ticketing-api:${{ github.sha }} .
```

### .github/workflows/cd.yml
```yaml
name: CD

on:
  push:
    branches: [main]

jobs:
  deploy-staging:
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Publish
        run: dotnet publish src/4-API/EnterpriseTicketing.API/EnterpriseTicketing.API.csproj -c Release -o ./publish
      - name: Deploy to Azure Web App (staging slot)
        uses: azure/webapps-deploy@v3
        with:
          app-name: enterprise-ticketing-api
          slot-name: staging
          package: ./publish
      - name: Swap slots (staging → production)
        uses: azure/CLI@v2
        with:
          inlineScript: |
            az webapp deployment slot swap \
              --resource-group ${{ vars.AZURE_RESOURCE_GROUP }} \
              --name enterprise-ticketing-api \
              --slot staging \
              --target-slot production
```

---

## Documentation Files

### README.md — Enterprise-grade main README:
Include:
- Project title and description
- Architecture overview diagram (ASCII art)
- Technology stack table
- Quick start instructions
- Milestone descriptions
- Prerequisites
- Local development setup
- Testing guide
- API documentation link
- Contributing guidelines
- License

### ARCHITECTURE.md — Comprehensive architecture document (all 20 topics from the documentation requirements section):
This is the most important document. Write it as if a senior architect is mentoring a team of engineers. Include:

1. Full architecture overview with Clean Architecture explanation
2. Request flow end-to-end (HTTP request → Middleware → Controller → MediatR → Handler → Repository → Dataverse → Response)
3. Authentication flow (JWT Bearer → Azure AD validation → Claims extraction → Authorization policies)
4. Dataverse SDK flow vs Web API flow comparison with when to use each
5. Power Platform integration flow
6. Azure Service Bus flow (publish → consume → dead letter)
7. Azure Functions event-driven flow
8. Dependency Injection registration strategy
9. Middleware pipeline order explanation (order matters!)
10. Logging architecture (Serilog + Application Insights + correlation)
11. Exception handling strategy (domain → application → infrastructure → API layer)
12. Configuration strategy (appsettings → env vars → Key Vault → Managed Identity)
13. Security architecture (OAuth2 → JWT → API keys → Dataverse OAuth)
14. Scalability considerations
15. Production support (health checks, dashboards, alerting)
16. Tradeoff analysis (SDK vs Web API, sync vs async, monolith vs microservices)
17. Evolution roadmap
18. What makes this enterprise-grade (vs junior/tutorial approach)
19. Common production mistakes avoided
20. How enterprise systems evolve over time

### docs/api/postman-collection.json
Full Postman collection with:
- Environment variables (baseUrl, tenantId, clientId, clientSecret, scope)
- Pre-request script for OAuth2 token acquisition
- All API endpoints with example request/response bodies
- Tests for each endpoint (status code, response schema)
- Organized in folders by resource

---

## Code Quality Standards

For ALL C# files:
- Use file-scoped namespaces
- Use primary constructors where appropriate (.NET 10)
- Prefer records for DTOs and commands
- Use `sealed` for classes not designed for inheritance
- Use `required` keyword for non-optional properties
- Use nullable reference types (`#nullable enable`)
- Async methods always end in `Async`
- Never use `async void` except event handlers
- Use `CancellationToken` in all async public methods
- Use `ILogger<T>` generic logger
- Log at appropriate levels (Debug/Information/Warning/Error/Critical)
- Never catch and swallow exceptions silently
- Use `ArgumentNullException.ThrowIfNull()` for guard clauses
- Use pattern matching over type casting
- Use `ConfigureAwait(false)` in library code

---

## Key Architectural Decisions to Explain

In comments and documentation, explain:

1. **Why Clean Architecture?** — Dependency rule, testability, change isolation
2. **Why MediatR CQRS?** — Separates reads from writes, clean handler boundaries, cross-cutting behaviors
3. **Why Polly?** — Transient fault handling, enterprise resiliency
4. **Why Serilog?** — Structured logging, sink flexibility, enrichers
5. **Why MediatR Pipeline Behaviors?** — Cross-cutting concerns without inheritance
6. **Why separate Application interfaces?** — Dependency inversion, testability, mock-ability
7. **Why SDK vs Web API?** — Explain in comments in each implementation
8. **Why Service Bus for events?** — Decoupling, retry, dead-letter, guaranteed delivery
9. **Why API versioning?** — Enterprise API evolution without breaking changes
10. **Why RFC 7807 Problem Details?** — Standard error format, machine-readable

---

## FINAL TASK

After creating all files, generate a summary README with:
1. Complete file tree
2. Step-by-step local setup
3. Architecture diagram (ASCII)
4. What was built in each milestone
5. How to test each milestone

Then commit everything:
```bash
git add -A
git commit -m "feat: Enterprise Dataverse Reference Implementation - All 5 Milestones

- Milestone 1: Core Enterprise Foundation (Clean Architecture, Dataverse SDK, API)
- Milestone 2: Dataverse Web API Integration (OData, HttpClientFactory, token management)
- Milestone 3: Azure Service Bus Integration (async messaging, DLQ, idempotency)
- Milestone 4: Azure Functions Event-Driven Architecture (isolated worker, triggers)
- Milestone 5: Microservices Evolution Architecture (boundaries, APIM, KEDA)

Technology Stack:
- ASP.NET Core 10 Web API
- Microsoft.PowerPlatform.Dataverse.Client
- MediatR 12 (CQRS)
- FluentValidation
- Polly 8
- Serilog + Application Insights
- Azure Service Bus
- Azure Functions (.NET 10 Isolated)
- Power Platform Model-driven App
- Azure Bicep (IaC)
- GitHub Actions CI/CD"
```

When completely finished, run this command to notify:
openclaw system event --text "Done: Enterprise Dataverse Reference Implementation complete - all 5 milestones built at /home/work/projects/enterprise-dataverse" --mode now
