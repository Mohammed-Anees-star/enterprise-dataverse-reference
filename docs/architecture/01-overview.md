# 01 - Architecture Overview

The Enterprise Ticketing reference implementation is a Clean Architecture monolith that
exposes a ticket-management domain through three integration surfaces:

- ASP.NET Core Web API (HTTP/REST, the primary surface)
- Azure Functions (event-driven worker)
- Power Platform Model-driven App (Dataverse-native UI)

All three share the same Dataverse tables. The C# code and the MDA cannot drift apart
because they read and write the same columns.

## Layered structure

```
+------------------+   1-Domain
|  Entities, VOs,  |   Pure C#. No external references.
|  Events, Rules   |   Cycles forbidden; everything else
+--------+---------+   may reference this layer.
         ^
         | ITicketRepository, ICustomerRepository (interfaces)
         |
+--------+---------+   2-Application
|  CQRS handlers,  |   MediatR commands/queries, pipeline behaviors,
|  Validation,     |   Application interfaces (IDataverseService, IEventBus).
|  Behaviors       |   Depends only on Domain.
+--------+---------+
         ^
         | IDataverseService impl, IEventBus impl, repository impls
         |
+--------+---------+   3-Infrastructure
|  Dataverse SDK,  |   ServiceClient, HttpClient + DelegatingHandler,
|  HTTP, Polly,    |   Polly policies, Service Bus client.
|  Service Bus     |   Depends on Application + Domain.
+--------+---------+
         ^
         | DI registration via AddInfrastructure()
         |
+--------+---------+   4-API           +-------------------+   5-Functions
|  Controllers,    |                   |  Service Bus      |
|  Middleware,     |                   |  triggered        |
|  Swagger, Auth   |                   |  workers          |
+------------------+                   +-------------------+
```

Dependencies flow inward. Outer layers can reference inner ones; inner layers cannot
reference outer ones. This is enforced by `csproj` `ProjectReference` graph.

## Bounded context

Today the solution is a single bounded context (Tickets) with Customer as an aggregate
that is consumed by the Ticket aggregate. Milestone 5 explains how the system splits
along context boundaries when it outgrows a single deployable.
