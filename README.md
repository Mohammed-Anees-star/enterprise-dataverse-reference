# Enterprise Ticketing System
## ASP.NET Core 10 + Microsoft Dataverse + Power Platform Reference Implementation

> A production-grade enterprise reference implementation built to the standard of senior architects and principal engineers.

[![CI](https://github.com/your-org/enterprise-ticketing/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/enterprise-ticketing/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                   Enterprise Ticketing System                        │
│                                                                      │
│  ┌─────────────┐    ┌──────────────────────────────────────────┐   │
│  │   External  │    │        ASP.NET Core 10 Web API            │   │
│  │   Clients   │───►│  Clean Architecture | CQRS | Polly | JWT  │   │
│  └─────────────┘    └────────────────┬─────────────────────────┘   │
│                                      │                               │
│  ┌─────────────┐    ┌───────────────▼──────────────────────────┐   │
│  │  Power MDA  │    │          Microsoft Dataverse               │   │
│  │  (Ops UI)   │───►│  (Shared data layer — single source of truth)│ │
│  └─────────────┘    └────────────────┬─────────────────────────┘   │
│                                      │                               │
│  ┌──────────────┐   ┌───────────────▼──────────────────────────┐   │
│  │    Azure     │   │     Azure Service Bus + Functions          │   │
│  │  Key Vault   │   │  (Event-driven async processing)           │   │
│  └──────────────┘   └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 / ASP.NET Core 10 |
| Dataverse SDK | Microsoft.PowerPlatform.Dataverse.Client |
| Dataverse Web API | OData v4 / HttpClientFactory |
| Authentication | Azure AD / Entra ID — OAuth2 JWT Bearer |
| Token Management | MSAL (Microsoft.Identity.Client) |
| Messaging | Azure Service Bus |
| Serverless | Azure Functions v4 (Isolated Worker, .NET 10) |
| CQRS | MediatR 12 |
| Validation | FluentValidation 11 |
| Resilience | Polly 8 |
| Logging | Serilog + Application Insights |
| Observability | Azure Application Insights + Azure Monitor |
| Secrets | Azure Key Vault + Managed Identity |
| Infrastructure | Azure Bicep (IaC) |
| CI/CD | GitHub Actions |
| Containers | Docker + docker-compose |
| Tests | xUnit + FluentAssertions + Moq |

---

## Repository Structure

```
enterprise-ticketing/
├── README.md                          ← This file
├── ARCHITECTURE.md                    ← Comprehensive architecture documentation
├── .env.example                       ← Environment variable template
├── docker-compose.yml                 ← Local development containers
│
├── src/
│   ├── EnterpriseTicketing.sln        ← Visual Studio solution
│   ├── 1-Domain/                      ← Pure domain — zero external dependencies
│   │   └── EnterpriseTicketing.Domain/
│   │       ├── Entities/              Ticket, Customer, TicketComment
│   │       ├── Enums/                 TicketStatus, Priority, Category
│   │       ├── ValueObjects/          TicketNumber, EmailAddress
│   │       ├── Events/                Domain events (TicketCreated, etc.)
│   │       ├── Exceptions/            Domain-specific exceptions
│   │       └── Interfaces/            Repository contracts
│   │
│   ├── 2-Application/                 ← Business orchestration
│   │   └── EnterpriseTicketing.Application/
│   │       ├── Common/Behaviors/      MediatR pipeline: Validation, Logging, Performance
│   │       ├── Common/Exceptions/     Application-layer exceptions
│   │       ├── Common/Interfaces/     IDataverseService, IEventBus, ICurrentUserService
│   │       └── Tickets/               CQRS Commands + Queries with handlers
│   │
│   ├── 3-Infrastructure/              ← External systems integration
│   │   └── EnterpriseTicketing.Infrastructure/
│   │       ├── Dataverse/             SDK implementation + Web API implementation
│   │       ├── Messaging/             Azure Service Bus event bus + consumer
│   │       ├── Security/              JWT user service + Dataverse token provider
│   │       ├── Http/                  DelegatingHandler + Polly policies
│   │       └── BackgroundServices/    Hosted service for message processing
│   │
│   ├── 4-API/                         ← ASP.NET Core Web API
│   │   └── EnterpriseTicketing.API/
│   │       ├── Controllers/v1/        REST controllers (versioned)
│   │       ├── Controllers/v2/        API v2 evolution demonstration
│   │       ├── Middleware/            CorrelationId, ExceptionHandling, SecurityHeaders
│   │       ├── Models/                Request/Response models (separate from Application DTOs)
│   │       ├── Program.cs             Enterprise bootstrap
│   │       ├── appsettings.json       Base configuration (placeholder values)
│   │       └── Dockerfile             Multi-stage production Dockerfile
│   │
│   └── 5-Functions/                   ← Azure Functions (event processing)
│       └── EnterpriseTicketing.Functions/
│           ├── Functions/             Service Bus triggers + Dataverse webhook
│           └── host.json              Functions host configuration
│
├── tests/
│   ├── Domain.Tests/                  Fast domain unit tests (no infrastructure)
│   ├── Application.Tests/            Handler tests with mocked dependencies
│   └── API.IntegrationTests/         Full API integration tests
│
├── power-platform/
│   ├── README.md                      MDA vs Canvas decision guide
│   ├── dataverse-tables/             Table definitions (JSON)
│   ├── model-driven-app/             Sitemap, app definition
│   └── power-automate/               Flow definitions
│
├── infrastructure/
│   └── azure/                        Bicep IaC (App Service, Key Vault, Service Bus, App Insights)
│
├── docs/
│   └── architecture/                 20 architecture documentation topics
│
└── .github/
    └── workflows/
        ├── ci.yml                     Build, test, Docker build
        └── cd.yml                     Deploy to Azure staging + production
```

---

## Milestone Breakdown

| Milestone | What's included | Status |
|-----------|----------------|--------|
| **M1: Core Foundation** | Clean Architecture, Dataverse SDK, JWT auth, CQRS, validation, Serilog, health checks, Swagger, API versioning | ✅ Complete |
| **M2: Dataverse Web API** | OData HTTP client, token handler, Polly policies, batch operations, ETag support | ✅ Complete |
| **M3: Azure Service Bus** | Async event publishing, competing consumers, dead-letter strategy, idempotency | ✅ Complete |
| **M4: Azure Functions** | Isolated worker model, Service Bus trigger, Dataverse webhook, distributed tracing | ✅ Complete |
| **M5: Microservices Evolution** | Architecture documentation, evolution patterns, service boundaries, APIM, KEDA | ✅ Complete (in ARCHITECTURE.md) |

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Docker Desktop | Latest |
| Azure CLI | Latest |
| Visual Studio / Rider / VS Code | Latest |

**Azure Resources needed:**
- Azure AD / Entra ID tenant (free tier sufficient for dev)
- Power Platform / Dataverse environment (Developer plan — free)
- Azure Service Bus namespace (Standard tier)
- Azure App Service (for deployment)
- Azure Key Vault (for secrets)
- Azure Application Insights (for monitoring)

---

## Local Development Setup

### Step 1: Clone and restore

```bash
git clone https://github.com/your-org/enterprise-ticketing.git
cd enterprise-ticketing
dotnet restore src/EnterpriseTicketing.sln
```

### Step 2: Configure secrets

```bash
cd src/4-API/EnterpriseTicketing.API

# Copy and fill the environment template
cp ../../../.env.example .env

# Or use dotnet user-secrets (recommended for local dev)
dotnet user-secrets set "Dataverse:ClientSecret" "your-secret"
dotnet user-secrets set "Dataverse:ClientId" "your-app-client-id"
dotnet user-secrets set "Dataverse:Url" "https://yourorg.crm.dynamics.com"
dotnet user-secrets set "Dataverse:TenantId" "your-tenant-id"
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://..."
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureAd:ClientId" "your-api-client-id"
```

### Step 3: Dataverse Setup

1. Go to [make.powerapps.com](https://make.powerapps.com)
2. Create a new environment (Developer plan)
3. Create App Registration in Azure AD:
   - Redirect URIs: none (client credentials flow)
   - API permissions: `Dynamics CRM` → `user_impersonation`
4. In Dataverse, go to Settings → Security → Application Users
5. Create Application User with the App Registration's Client ID
6. Assign "System Administrator" security role (or custom role)
7. Create the custom tables from `power-platform/dataverse-tables/`

### Step 4: Run the API

```bash
cd src/4-API/EnterpriseTicketing.API
dotnet run
```

API available at: `https://localhost:5001`
Swagger UI: `https://localhost:5001/swagger`
Health check: `https://localhost:5001/health`

### Step 5: Run with Docker

```bash
# Build and start
docker-compose up --build

# API available at http://localhost:5000
```

---

## API Testing with Swagger

1. Open `https://localhost:5001/swagger`
2. Click **Authorize** → Enter Client Credentials:
   - Client ID: your Azure AD app client ID
   - Client Secret: your client secret
   - Scope: `api://YOUR_CLIENT_ID/.default`
3. Test endpoints:

```
POST /api/v1/tickets         — Create ticket
GET  /api/v1/tickets         — List tickets (with filters)
GET  /api/v1/tickets/{id}    — Get ticket by ID
PUT  /api/v1/tickets/{id}    — Update ticket
POST /api/v1/tickets/{id}/close    — Close ticket
POST /api/v1/tickets/{id}/escalate — Escalate ticket
```

### Example request body (POST /api/v1/tickets):

```json
{
  "title": "Production database connectivity issue",
  "description": "Users experiencing intermittent database connection failures since 14:30 UTC. Error: Connection timeout after 30s.",
  "priority": "Critical",
  "category": "TechnicalSupport",
  "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

## Running Tests

```bash
# All tests
dotnet test src/EnterpriseTicketing.sln

# Domain tests only (fast, no dependencies)
dotnet test tests/EnterpriseTicketing.Domain.Tests/

# Application tests
dotnet test tests/EnterpriseTicketing.Application.Tests/

# With coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

---

## Production Deployment

### Azure deployment with Bicep

```bash
# Create resource group
az group create -n rg-enterprise-ticketing-prod -l eastus

# Deploy all infrastructure
az deployment group create \
  -g rg-enterprise-ticketing-prod \
  -f infrastructure/azure/main.bicep \
  -p environmentName=prod appName=enterprise-ticketing

# Deploy application
dotnet publish src/4-API/EnterpriseTicketing.API/ -c Release -o ./publish
az webapp deploy --resource-group rg-enterprise-ticketing-prod \
  --name enterprise-ticketing-prod --src-path ./publish --type zip
```

### GitHub Actions (automated)

Push to `main` branch → CI → Deploy to staging → Smoke tests → Swap to production

Configure secrets in GitHub repository settings:
- `AZURE_CREDENTIALS` — Service Principal JSON
- `CODECOV_TOKEN` — Code coverage upload

---

## What Makes This Enterprise-Grade

1. **Dependency Rule enforced** — Domain has zero external dependencies
2. **Explicit failure modes** — Every exception type maps to a specific HTTP status
3. **Correlation threading** — Every log entry across every service carries the same ID
4. **Secrets never in code** — Key Vault + Managed Identity in production
5. **Graceful degradation** — Circuit breakers prevent cascade failures
6. **Idempotent operations** — Service Bus consumers are safe to retry
7. **Paginated everywhere** — No unbounded query results
8. **Observable from day one** — Structured logs, health checks, metrics
9. **Versioned API** — Consumers can upgrade at their own pace
10. **Infrastructure as Code** — Environments are reproducible, not snowflakes

---

## License

MIT License — See [LICENSE](LICENSE) for details.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

Architecture questions? See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed explanations of every design decision.
