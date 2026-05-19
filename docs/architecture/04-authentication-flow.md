# 04 - Authentication Flow

JWT Bearer over OAuth2 against Microsoft Entra ID.

```
Client -> /oauth2/v2.0/authorize (Entra ID)
       <- code
Client -> /oauth2/v2.0/token       -> id_token + access_token (JWT)
Client -> /api/v1/tickets (Authorization: Bearer <jwt>)

ASP.NET Core (Microsoft.Identity.Web):
  - Fetches OIDC discovery doc (cached 24h)
  - Validates JWT signature with rotating JWKS keys
  - Validates issuer, audience, exp, nbf, signing alg
  - On success: sets HttpContext.User with claims (oid, roles, scp)
```

App-to-app calls (Functions -> API) use the client credentials grant with a
managed identity; no client secret is stored anywhere.

Authorization policies in `AuthenticationConfiguration.cs`:
- `TicketRead`  - any authenticated user
- `TicketWrite` - role TicketAgent/Manager/Administrator
- `TicketAdmin` - role Manager/Administrator
