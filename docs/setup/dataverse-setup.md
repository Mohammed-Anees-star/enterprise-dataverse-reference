# Dataverse Setup

1. Provision a Dataverse environment (Production type for prod; Sandbox for non-prod).
2. Register an Entra ID app for the API: Application + Delegated permissions to
   `https://<your-org>.crm.dynamics.com/user_impersonation` and create an Application user
   in Power Platform admin centre.
3. Grant the application user the `Ticket Manager` security role
   (see `power-platform/security-roles/ticket-manager-role.json`).
4. Import the solution package from `power-platform/`:
   ```pwsh
   pac auth create --url https://<your-org>.crm.dynamics.com
   pac solution import --path enterprise-ticketing-solution.zip
   ```
5. Verify the tables `new_ticket`, `new_customer`, `new_ticketcomment` appear in the
   environment.

## Wiring secrets

The `DATAVERSE__CLIENTSECRET` env var is for local dev only. In production, the secret
lives in Azure Key Vault and is referenced from App Service settings via
`@Microsoft.KeyVault(SecretUri=...)`.
