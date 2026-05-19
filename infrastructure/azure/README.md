# Azure Infrastructure (Bicep)

Bicep templates deploy the Azure footprint for the Enterprise Ticketing reference solution.

## Resources

| Resource | Purpose | SKU (prod) |
|---|---|---|
| App Service Plan + Web App | Hosts the ASP.NET Core API | P2v3, zone redundant |
| Azure Functions (Linux Consumption) | Event consumers | Y1 |
| Service Bus namespace | Async messaging | Premium (for zone redundancy & VNet integration) |
| Key Vault | Secrets & cert store | Standard, soft-delete + purge protection |
| Application Insights | Telemetry & APM | Workspace-based |

## Layout

| File | Purpose |
|---|---|
| `main.bicep` | Root composition - calls every module |
| `app-service.bicep` | Plan, web app, deployment slots, app settings, MI |
| `service-bus.bicep` | Namespace + ticket-events / ticket-notifications queues |
| `key-vault.bicep` | Vault + diagnostic settings |
| `key-vault-access.bicep` | Role assignments granting MI access |
| `app-insights.bicep` | Workspace-based AI resource |

## Deploy

```bash
az group create --name rg-enterprise-ticketing-prod --location eastus2

az deployment group create \
  --resource-group rg-enterprise-ticketing-prod \
  --template-file main.bicep \
  --parameters environmentName=prod
```

## Identity & Access

The Web App uses a system-assigned managed identity. Key Vault role assignments are
granted via `key-vault-access.bicep`. No connection strings or client secrets live in
App Settings - everything sensitive is a `@Microsoft.KeyVault(SecretUri=...)` reference.

The Dataverse app registration is provisioned out of band (Power Platform does not have
first-class Bicep support); its client id and tenant id are written into Key Vault and
the client secret is rotated via an Azure Automation runbook.

## Observability

`app-insights.bicep` provisions a workspace-based Application Insights resource attached
to the central Log Analytics workspace. Diagnostic settings on every other resource pipe
into the same workspace so that the workbook in `docs/architecture/18-production-support.md`
queries the whole stack in one place.
