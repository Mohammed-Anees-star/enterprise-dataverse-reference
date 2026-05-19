# 20 - Evolution Roadmap

This solution is designed to evolve, not to be rewritten. The progression below is the
expected path; each step is independent so teams can choose to stop or skip a step.

## Now (Milestones 1-4)
- Monolith API + worker functions
- Single Dataverse environment
- Application Insights for observability

## +6 months
- API Management front door (rate limit, JWT validation at edge)
- Read-model projection in Cosmos DB for high-volume dashboards
- Outbox table backing the Service Bus publish step

## +12 months
- Notification service extracted (own database, own deploy cadence)
- Reporting service extracted (CQRS read side over Synapse / Fabric)
- Customer service extracted (the natural seam)

## +18 months
- KEDA-driven autoscale on Container Apps
- Multi-region active-active for the API
- Dataverse environments per region with Power Platform CoE governance
