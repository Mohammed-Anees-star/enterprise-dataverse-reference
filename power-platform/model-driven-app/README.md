# Model-driven App

This folder describes the Model-driven App (MDA) that runs alongside the ASP.NET Core API.
Both surfaces operate on the same Dataverse tables defined in `../dataverse-tables/`.

## Files

| File | Purpose |
|---|---|
| `app-definition.json` | High-level metadata: name, components, security roles |
| `sitemap.xml` | Left-rail navigation for support agents |

## Why a Model-driven App?

The MDA is the right choice when the experience is:

- **Internal** — agents and managers, not customers
- **Record-centric** — the user mostly clicks through related tables (Ticket -> Customer -> Comments)
- **Form-driven** — heavy use of business rules, role-based field-level security, lookups
- **Configuration-first** — most changes should be no-code (forms, views, business rules, dashboards)

A Canvas App would be a better fit if the experience were customer-facing with pixel-perfect UX,
mobile-first, or had complex cross-system integrations as its primary purpose.

## Deployment

The components are imported as part of a Power Platform Solution package:

```pwsh
pac solution import --path .\EnterpriseTicketing.zip --environment <env-url>
```

CI/CD with Power Platform Build Tools handles export from a Dev environment, packaging,
and import to UAT/Prod. See `infrastructure/scripts/deploy-azure.sh` and the workflow in
`.github/workflows/cd.yml` for the end-to-end picture.
