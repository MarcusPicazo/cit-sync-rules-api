# CIT-Sync Scheduling Rules API

## Context
CIT-Sync (github.com/MarcusPicazo/cit-sync) is a React + Firebase platform for Escuela Berta Von Glümer:
spaces/labs, hardware inventory and reservations. Today ALL booking rules live in the browser
(`src/features/dashboard/components/BookingModal.jsx`, `src/features/admin/AdminPanelView.jsx`):

- only client-side checks (end > start, matrícula / número de empleado required)
- no overlap/conflict detection between reservations
- no capacity check against `resource.quantity`
- status changes and deletes go straight to Firestore (`updateDoc` / `deleteDoc`) with no audit trail
- rules such as subjects or allowed hours are hardcoded

IMPORTANT: github.com/MarcusPicazo/cit-sync is the PUBLIC DEMO the owner has permission to share. The real
school system is private. Use only the demo and fictional seed data (no real names, students or school data),
and never reference the private codebase.

This repo moves that logic server-side into a **C# / ASP.NET Core rules service** that CIT-Sync (and any
other school app) calls to create, version, approve and evaluate scheduling rules.

## Owner learning goal (important)
The owner is preparing for a C#/.NET interview. After each phase:
1. Explain in Spanish what was built, why, and which .NET concept it demonstrates.
2. Leave 1–2 small TODOs marked `// TODO(Marcus):` for him to implement by hand, then review his code.
3. Never commit secrets; use user-secrets / env vars.

## Stack
- .NET 8 LTS or newer (use the newest LTS installed), ASP.NET Core Web API with **controllers**
- API versioning: `Asp.Versioning.Mvc` (v1 now, v2 later), OpenAPI/Swagger
- EF Core + **SQL Server** (Docker: `mcr.microsoft.com/mssql/server`), code-first migrations
- FluentValidation, ProblemDetails (RFC 7807) for all errors
- Tests: xUnit + NSubstitute + FluentAssertions; integration tests with Testcontainers.MsSql + WebApplicationFactory
- Serilog (structured logs), OpenTelemetry tracing, health checks (`/health`)
- GitHub Actions CI: build + test on every PR

## Solution layout
```
src/CitSync.Rules.Api            controllers, DI, middleware
src/CitSync.Rules.Application    use cases, validators, rule evaluation engine
src/CitSync.Rules.Domain         entities, enums, domain logic (no EF references)
src/CitSync.Rules.Infrastructure EF Core DbContext, configurations, migrations, repositories
tests/CitSync.Rules.UnitTests
tests/CitSync.Rules.IntegrationTests
docs/adr/                        architecture decision records
docs/runbook.md
```

## Domain
- **Resource**: lab, classroom, equipment (id, name, category, capacity, building, isActive)
- **Reservation**: resourceId, requester (role: alumno | profesor | admin), start/end (UTC + school TZ America/Mexico_City),
  quantity, subject, status (Pending → Approved | Rejected | Cancelled), soft delete only
- **RuleSet** with versions: a **RuleSetVersion** has state Draft → PendingApproval → Active → Retired.
  Only one Active version per scope (school / building / resource). Activating a new one retires the previous.
- **Rule** types (start with these):
  - `OpeningHours` (per weekday)
  - `MaxDuration` (minutes, per role)
  - `NoOverlap` (per resource, considering quantity/capacity)
  - `RoleRestriction` (e.g. alumnos cannot book equipment X)
  - `BlackoutDates` (holidays, exam days)
  - `LeadTime` (must book ≥ N hours ahead)
- **AuditEntry**: entity, entityId, action, actor, timestamp, before/after JSON. Written automatically
  via EF Core SaveChanges interceptor.
- Optimistic concurrency with `rowversion` on Reservation and RuleSetVersion (return 409 on conflict).

## Endpoints (v1)
- `GET/POST /api/v1/resources`
- `GET/POST /api/v1/rulesets`, `POST /api/v1/rulesets/{id}/versions`
- `POST /api/v1/rulesets/{id}/versions/{v}/submit` | `/approve` | `/reject`
- `POST /api/v1/rules/evaluate` → body: a proposed reservation; returns `{ allowed, violations[] }`
- `GET/POST /api/v1/reservations`, `POST /api/v1/reservations/{id}/approve|reject|cancel`
- `GET /api/v1/audit?entity=&entityId=`

## SQL / performance
- Index reservations on (ResourceId, StartUtc, EndUtc) filtered by non-deleted, non-cancelled.
- Overlap query must be a single sargable query; document it in `docs/query-tuning.md` with the execution plan.
- Use `AsNoTracking` for reads, pagination on lists.

## Phases
1. Solution skeleton, Docker SQL Server, DbContext, first migration, health check, Swagger, CI.
2. Resources + Reservations CRUD with validation and ProblemDetails.
3. RuleSets with versioning + approval state machine + audit interceptor + concurrency.
4. Rule evaluation engine (strategy pattern, one class per rule type) + `/evaluate`.
5. Unit + integration tests (target: every rule type and state transition covered).
6. Serilog, OpenTelemetry, query tuning doc, runbook, ADRs, README with AI-usage section.
7. Integrate CIT-Sync: BookingModal calls `/evaluate` before saving; AdminPanel approve/reject goes through the API.

## Conventions
- Async all the way, CancellationToken on every endpoint
- No business logic in controllers
- Commit per phase with clear messages
- Work on the E: drive; never put node_modules, bin/obj or Docker volumes on C:
