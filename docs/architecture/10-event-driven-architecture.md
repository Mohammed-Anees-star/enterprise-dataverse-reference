# 10 - Event-Driven Architecture

Domain events stay in-process; integration events cross the wire on Service Bus.

| Layer | Event kind | Transport | Example |
|---|---|---|---|
| Domain | Domain event | In-memory, via MediatR INotification | TicketCreatedEvent |
| Application | Integration event | Service Bus | TicketCreatedV1 |
| Infrastructure | System event | Application Insights custom event | DataverseTokenRefreshed |

Outbox pattern: domain events are collected on the aggregate, persisted alongside
the entity in the same logical transaction (Dataverse single-Create call), and
dispatched to Service Bus AFTER the persistence succeeds. This avoids the
dual-write problem (event published but DB rolled back).

In a future evolution we'd back this with a dedicated outbox table to recover
from the rare crash between Dataverse-write and bus-publish.
