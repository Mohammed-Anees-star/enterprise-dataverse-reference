# Power Platform — Enterprise Ticketing

## Architecture Overview

The Power Platform and ASP.NET Core API share the **same Dataverse tables**.
This is the key architectural insight: there is a single source of truth (Dataverse),
and multiple consumers — the API for programmatic access, the Model-driven App for
operations staff.

```
┌─────────────────────────────────────────────────────────────┐
│                    Microsoft Dataverse                       │
│  ┌──────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │ new_ticket   │  │  new_customer  │  │ new_comment    │  │
│  └──────┬───────┘  └───────┬────────┘  └───────┬────────┘  │
└─────────┼──────────────────┼───────────────────┼───────────┘
          │                  │                   │
  ┌───────▼───────┐  ┌───────▼───────────────────▼──────────┐
  │ ASP.NET Core  │  │    Power Platform Model-driven App   │
  │  Web API      │  │    (Operations staff UI)              │
  │  (SDK +       │  │    Business rules, forms, views       │
  │  Web API)     │  │    Power Automate flows               │
  └───────────────┘  └──────────────────────────────────────┘
```

---

## Why Model-Driven App (vs Canvas App)?

### Choose Model-Driven App when:
- Data model is complex with many related tables
- Standard CRUD operations are primary use case
- Power Platform security roles (record-level access) are needed
- Offline capability is required (mobile)
- Business rules, field validation, and calculated columns are needed
- Rapid development for internal/operations users
- Data is Dataverse-native
- Standard forms, views, dashboards are sufficient

### Choose Canvas App when:
- Custom UX is critical (pixel-perfect branding)
- Consumer-facing experience (external users)
- Complex multi-screen flows with custom navigation
- Data sources outside Dataverse (SharePoint, SQL, custom APIs)
- Mobile-first design with specific gesture interactions
- Gallery layouts, custom charts, embedded media

### This solution uses Model-Driven App because:
The ticket management use case is operations-focused (support agents, managers).
The rich Dataverse table structure (relationships, business rules, security roles)
makes Model-Driven App the natural choice. Canvas would require reimplementing
all the behaviors already built into the platform.

---

## How Model-Driven App Works with Dataverse

1. **Tables** — Each table (new_ticket, new_customer) maps to a Model-Driven App entity
2. **Forms** — Main Form displays all ticket fields with sections and tabs
3. **Views** — Public views filter/sort records (Active Tickets, My Tickets, High Priority)
4. **Business Rules** — Client-side JavaScript-free rules (e.g., make Resolution Notes required when Status = Resolved)
5. **Security Roles** — Row-level and column-level security without code
6. **Dashboards** — Aggregate views, charts, KPIs

---

## How the ASP.NET Core API and Model-Driven App Share Data

Both access the **same Dataverse tables**:

| Operation | ASP.NET Core API | Model-Driven App |
|-----------|------------------|-----------------|
| Create ticket | POST /api/v1/tickets → Dataverse SDK | User fills form → saves directly |
| View tickets | GET /api/v1/tickets → FetchXML | Opens Active Tickets view |
| Update status | POST /api/v1/tickets/{id}/close | Agent changes Status dropdown |
| Escalate | POST /api/v1/tickets/{id}/escalate | Manager clicks Escalate button |

Records created via the API appear immediately in the Model-Driven App.
Records updated in the app are immediately visible via the API.
Power Automate flows trigger on both API-created and app-created records.

---

## Dataverse Tables

### new_ticket
| Column | Type | Notes |
|--------|------|-------|
| new_ticketid | Primary Key | Auto-generated GUID |
| new_ticketnumber | Text (20) | TKT-YYYY-NNNNNN format |
| new_title | Text (200) | Required |
| new_description | Multiline Text | |
| new_status | Choice | Open/InProgress/PendingCustomer/PendingThirdParty/Resolved/Closed/Cancelled |
| new_priority | Choice | Low/Medium/High/Critical |
| new_category | Choice | TechnicalSupport/Billing/AccountManagement/FeatureRequest/Bug/GeneralInquiry/SecurityIncident |
| new_customerid | Lookup → new_customer | Required |
| new_assignedtouserid | Text (100) | Azure AD User Object ID |
| new_resolvedat | Date/Time | Set when status = Resolved |
| new_closedat | Date/Time | Set when status = Closed |
| new_escalationcount | Whole Number | Default 0 |
| new_resolutionnotes | Multiline Text | Required on resolve |

### new_customer
| Column | Type | Notes |
|--------|------|-------|
| new_customerid | Primary Key | |
| new_fullname | Text (200) | Primary Name Column |
| new_email | Email | Unique |
| new_phonenumber | Phone | |
| new_companyname | Text (200) | |
| new_accountnumber | Text (50) | |
| new_isactive | Yes/No | Default Yes |

---

## Security Roles

### Ticket Agent
- Read: All active tickets
- Write: Own assigned tickets (update status, add comments)
- Create: New tickets
- Delete: None

### Ticket Manager
- Read: All tickets
- Write: All tickets (including escalate, close, reassign)
- Create: Tickets
- Delete: Soft-delete (cancel status only)

### Administrator
- Full access to all tables and configuration

---

## Power Automate Flows

### 1. Ticket Created — Customer Notification
- Trigger: When a row is added (new_ticket)
- Get customer email from new_customerid lookup
- Send confirmation email: "Your ticket TKT-YYYY-NNNNNN has been received"
- If Priority = Critical: Post to Teams channel "Critical Ticket Alert"

### 2. SLA Breach Warning
- Trigger: Scheduled (every 30 minutes)
- Query: Open/InProgress tickets created > 4 hours ago with Priority = High or Critical
- For each: Post Teams notification to ticket manager channel
- Update ticket with escalation flag

### 3. Ticket Resolved — CSAT Survey
- Trigger: When status changes to Resolved
- Wait 24 hours
- Send CSAT survey email to customer
- Create survey response record linked to ticket
