# Local Development

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for the API and Functions containers)
- Azure CLI (`az`) signed in to the tenant
- Power Platform CLI (`pac`) for solution import (optional)
- A Dataverse environment you have System Customizer rights on

## Steps

1. `cp .env.example .env` and fill in real values for your dev environment.
2. `./infrastructure/scripts/setup-local-dev.sh`
3. Open `http://localhost:5000/swagger` and click "Authorize" to acquire a token.
4. Try the `POST /api/v1/tickets` endpoint.

## Running tests

```bash
dotnet test src/EnterpriseTicketing.sln
```

The Dataverse and Service Bus integration tests are gated by environment variables; they
skip cleanly in CI unless `INTEGRATION_TESTS=true` is set.
