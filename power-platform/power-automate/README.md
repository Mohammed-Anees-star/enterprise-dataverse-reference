# Power Automate Flows

Cloud flows declared in this folder are imported via the Power Platform CLI as part of
the solution package.

| Flow | Trigger | Purpose |
|---|---|---|
| `ticket-created-flow.json` | Dataverse row added on `new_ticket` | Customer confirmation, Teams alert on Critical, escalation alert, initial system comment |

## When to use Power Automate vs Azure Functions

Both can react to the same Dataverse event, but each is best at different things:

- **Power Automate** for citizen-developer-friendly orchestrations involving Office 365
  connectors (Outlook, Teams, SharePoint, Approvals). Cheap to author, fully managed,
  great visibility for non-engineers.
- **Azure Functions** for code-heavy logic, custom integrations, high throughput, or
  anywhere you need full control over retry, error handling, and connection pooling.

The reference solution uses both: Power Automate for customer-visible workflows that a
support manager can reasonably modify; Azure Functions for the message bus consumers,
plugins, and integrations with internal systems.
