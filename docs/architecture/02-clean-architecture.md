# 02 - Clean Architecture

We follow the four-ring Clean Architecture (Robert C. Martin, 2012) with a fifth
adapter ring for Azure Functions.

## The dependency rule

Source-code dependencies point only inward. A class in Application can use a Domain
type; a Domain class cannot reference Application. This is enforced by the project
reference graph and a build-time check in CI (`dotnet list reference` parsed against
an allow-list).

## Why this matters in practice

- **Testability**: handlers depend on `ITicketRepository`, not on `ServiceClient`.
  Unit tests substitute Moq doubles - no Dataverse calls in CI.
- **Change isolation**: replacing Dataverse with SQL or Cosmos affects only
  the Infrastructure layer; handlers and controllers do not change.
- **Mental model**: a new engineer can read the Domain in isolation and know
  *what* the system does without paging in *how* it does it.

## Layer responsibilities

| Layer | Allowed to know about |
|---|---|
| Domain | Itself only |
| Application | Domain |
| Infrastructure | Domain, Application |
| API | Application (and Infrastructure for DI wiring only) |
| Functions | Application + Infrastructure |

## Anti-patterns we deliberately avoid

- **Anaemic domain model**: domain entities own behaviour (`Ticket.Escalate(...)`),
  not just data.
- **Repository-of-everything**: each aggregate root has exactly one repository.
- **Leaky abstractions**: `IDataverseService` exposes `Dictionary<string, object>`
  not `Microsoft.Xrm.Sdk.Entity` so Application stays infrastructure-agnostic.
