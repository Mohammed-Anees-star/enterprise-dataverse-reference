# 07 - Power Platform Integration

The API and the Model-driven App share Dataverse tables byte-for-byte.

```
                  +-----------------+
                  |  Dataverse      |
                  |  (single source |
                  |   of truth)     |
                  +--------+--------+
                           ^
            +--------------+---------------+
            |              |               |
+-----------+----+   +-----+------+   +----+-------+
| ASP.NET Core   |   | Model-     |   | Power      |
| Web API        |   | driven App |   | Automate   |
| (Milestone 1+) |   | (canvas)   |   | (cloud     |
|                |   |            |   |  flows)    |
+-----------+----+   +------------+   +------------+
            |
            v
       Service Bus -> Azure Functions -> downstream systems
```

The MDA handles the "agent productivity" workflows that benefit from no-code forms,
business rules, and Dataverse-native security. The API handles programmatic access from
customer-facing front ends, mobile apps, and internal integrations.

Plugins/webhooks on Dataverse tables can POST to `DataverseWebhookFunction` which then
publishes a Service Bus event to keep both surfaces in sync.
