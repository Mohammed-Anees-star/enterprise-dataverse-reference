# 06 - Dataverse Web API Flow

The Web API uses standard OData v4 over HTTPS at `/api/data/v9.2/`.

Pipeline:
```
DataverseWebApiService -> IHttpClientFactory("DataverseWebApi")
                            │
                            ▼
                       DataverseHttpClientHandler (DelegatingHandler)
                            │  - acquires bearer token from DataverseTokenProvider
                            │  - adds OData-MaxVersion / OData-Version / Prefer headers
                            ▼
                       Polly wrap policy (CircuitBreaker + Retry)
                            │
                            ▼
                       Dataverse OData endpoint
```

Use the Web API when:
- Calling from a non-.NET service (the SDK is C#-only)
- You need OData query semantics ($filter, $expand, $count)
- You want HTTP-native tooling (Postman, fiddler) for debugging
- You need OData $batch for transactional multi-step writes

Token caching is handled by `DataverseTokenProvider` (MSAL ConfidentialClientApplication
with a 5-minute safety buffer ahead of the actual expiry).
