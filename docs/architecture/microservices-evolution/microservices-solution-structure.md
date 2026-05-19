# Microservices Solution Structure

Today the bounded contexts live as folders inside a single solution. The future state is:

```
enterprise-ticketing/
├── services/
│   ├── ticket-service/
│   │   ├── src/{Domain,Application,Infrastructure,API}
│   │   ├── tests/
│   │   └── infra/  (per-service bicep)
│   ├── customer-service/
│   ├── notification-service/
│   ├── integration-service/
│   └── reporting-service/
├── libraries/
│   ├── shared-contracts/      (integration event schemas)
│   ├── shared-resilience/     (Polly policies)
│   └── shared-observability/  (Serilog enrichers, AI starters)
└── platform/
    ├── api-gateway/           (APIM config)
    ├── observability/         (workbooks, alerts, dashboards)
    └── networking/
```

Migration steps from today's monolith:

1. Lift TicketService bounded context into its own solution; its Service Bus events stay
   the same. Front it with APIM. The original monolith calls TicketService over HTTP.
2. Repeat for CustomerService; the moment Customer reads come from CustomerService,
   the monolith's CustomerRepository becomes a thin REST client.
3. Notification side-effects already run via Service Bus - extracting NotificationService
   is purely a deploy-target change.
4. ReportingService stands up independently; it subscribes to all integration events to
   build read models.
5. The original monolith shrinks to a thin BFF (backend-for-frontend) for the MDA and
   eventually retires.
