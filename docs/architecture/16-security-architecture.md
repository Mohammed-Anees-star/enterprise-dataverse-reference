# 16 - Security Architecture

Identity stack:
- End users: OAuth2 Authorization Code + PKCE against Entra ID
- Service-to-service: Client Credentials with a managed identity
- API to Dataverse: Client Credentials against the Dataverse-registered app

Transport: TLS 1.2 minimum, enforced at App Service and APIM.

In-flight security:
- HSTS, X-Content-Type-Options, X-Frame-Options, CSP via SecurityHeadersMiddleware
- Cross-origin allow-list driven by `Cors:AllowedOrigins` in config

At-rest security:
- Dataverse encryption-at-rest (Microsoft-managed keys, optional CMK)
- Key Vault for any secret not stored in Dataverse
- No connection strings in app settings - everything is a Key Vault reference

Authorization layers:
- Entra ID role/scope claim -> ASP.NET policy ("TicketWrite" etc.)
- Dataverse field-level security for data the API exposes via the SDK
