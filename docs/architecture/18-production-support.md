# 18 - Production Support

Three pillars:

## Health checks

| Endpoint | Purpose | Used by |
|---|---|---|
| /health/live  | Process is up                 | App Service / k8s liveness probe |
| /health/ready | Dependencies reachable        | App Service / k8s readiness probe |
| /health       | Full snapshot incl. degraded  | Dashboards |

## Dashboards

Application Insights workbook published in the central Log Analytics workspace.
Tiles:
- Requests by route, status code, p95 latency
- Dependency failure rate by target (Dataverse, Service Bus)
- Custom metric: tickets_created_total, tickets_escalated_total
- Live SLA burn-rate for Critical priority

## Alerting

Action groups -> PagerDuty + Teams channel.

Alert rules (Azure Monitor):
- Availability < 99.5% over 5 min
- p95 latency > 1500 ms over 10 min
- Service Bus DLQ depth > 0
- Failed dependency rate to Dataverse > 5%
- Memory pressure > 85% sustained 15 min
