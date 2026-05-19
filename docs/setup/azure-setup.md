# Azure Setup

Detailed Bicep deployment is in `infrastructure/azure/README.md`. This page covers the
one-time prerequisites.

1. Subscription. Tag with `cost-center=platform-engineering`.
2. Resource group per environment (`rg-enterprise-ticketing-{env}`).
3. Entra ID app registrations:
   - One for the API (consumed by user-facing clients)
   - One for the Dataverse access (client-credentials)
4. Service Principal + GitHub Actions OIDC trust (no client secret stored anywhere).
5. Key Vault provisioned via `key-vault.bicep`. Seed the secrets:
   - `DataverseClientSecret`
   - `ServiceBusConnectionString`
   - `ApplicationInsightsConnectionString`
6. Run `infrastructure/scripts/deploy-azure.sh` with `ENVIRONMENT=prod`.

## Slot-based deployments

The Web App has a `staging` slot. GitHub Actions `cd.yml` deploys there first, runs
smoke tests, then swaps to production. Auto-swap is OFF on purpose - swaps are gated
on a manual approval in the deployment environment.
