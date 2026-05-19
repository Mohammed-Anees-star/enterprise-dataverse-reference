# 17 - Scalability

Horizontal scaling is the default; vertical scaling is the override.

| Tier | Scale unit | Trigger |
|---|---|---|
| API (App Service)  | Instances | CPU > 70%, http-queue > 100 |
| Functions          | Workers   | KEDA scale rule on Service Bus queue length |
| Service Bus        | Premium messaging units | manual; PMUs are coarse-grained |
| Dataverse          | Capacity add-on | DB size / API request count |

Stateless processes: no in-memory session state. Idempotency caches are MemoryCache
locally for dev but Redis-backed when more than one instance runs.

Cold-paths to watch:
- Token acquisition - MSAL cache is per-instance; warm with a `WarmupHostedService`.
- Dataverse first call - ServiceClient runs a metadata fetch on first request.
