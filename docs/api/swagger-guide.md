# Swagger / OpenAPI

The API exposes Swagger at `/swagger` in non-production environments. The OpenAPI 3.0
document for v1 is at `/swagger/v1/swagger.json`; v2 lives at `/swagger/v2/swagger.json`.

## Authorising in Swagger UI

1. Click "Authorize"
2. Enter the OAuth2 client id (from `AzureAd:ClientId`)
3. Click "Authorize" and complete the consent flow with your Entra ID account
4. The token is attached to every Try-It-Out request

## Versioning convention

URL-segment versioning (`/api/v1/...`) is the primary discoverable form; we also accept
the `X-Api-Version` header for clients that cannot rewrite URLs.

Breaking changes go to a new version. The previous version is supported for one
deprecation period (12 months by default).
