# 15 - Configuration Strategy

Config sources, in order of precedence (later wins):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables (`Section__Key=value`)
4. User secrets (Development only)
5. Azure Key Vault (Production - referenced via App Service Key Vault references)

Strongly typed via `IOptions<T>` with DataAnnotations validation at startup.
`builder.Services.AddOptions<DataverseConfiguration>().ValidateDataAnnotations().ValidateOnStart()`
fails fast if mandatory keys are missing.

Secrets:
- Local dev: `dotnet user-secrets` for the API project.
- Production: Key Vault refs in App Service Settings - `@Microsoft.KeyVault(SecretUri=...)`.
- Functions: same pattern via the Functions app settings.
