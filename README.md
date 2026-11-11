# CIT-Sync Scheduling Rules API

A **C# / ASP.NET Core** service that moves the booking rules of
[CIT-Sync](https://github.com/MarcusPicazo/cit-sync) from the browser to the server.

> **What this is:** a personal extension project built on top of the **public CIT-Sync demo**
> (React + Firebase: labs, classrooms, equipment and reservations). It only uses the demo and
> fictional seed data. No real school, student or staff data is used here.

In the demo, every rule lives in the browser (`BookingModal.jsx`, `AdminPanelView.jsx`):
client-side checks only, no overlap detection, no capacity check, and status changes go straight
to Firestore with no audit trail. This API centralizes those rules so CIT-Sync (or any other school
app) can create, version, approve and evaluate them.

## Stack

- .NET 10 (LTS), ASP.NET Core Web API with controllers, API versioning (`/api/v1`), Swagger
- EF Core 10 + SQL Server 2022 (Docker), code-first migrations
- FluentValidation, ProblemDetails (RFC 7807) for every error
- xUnit, NSubstitute, FluentAssertions; integration tests with Testcontainers + WebApplicationFactory
- GitHub Actions: build + test on every PR

```
src/CitSync.Rules.Api             controllers, DI, error handling
src/CitSync.Rules.Application     use cases, DTOs, validators (rule engine in phase 4)
src/CitSync.Rules.Domain          entities and domain logic (no EF references)
src/CitSync.Rules.Infrastructure  EF Core DbContext, configurations, migrations, repositories
tests/                            unit and integration tests
```

## Running locally

Requirements: .NET 10 SDK, Docker Desktop, and the EF tool (`dotnet tool install -g dotnet-ef`).

1. **Start SQL Server.** Copy `.env.example` to `.env` and set a strong `MSSQL_SA_PASSWORD`
   (`.env` is git-ignored):

   ```bash
   cp .env.example .env
   docker compose up -d --wait
   ```

   SQL Server listens on `localhost:14333`.

2. **Set the connection string** with user-secrets. It is never stored in `appsettings.json`.
   Outside development, use the `ConnectionStrings__RulesDb` environment variable instead.

   ```bash
   dotnet user-secrets set "ConnectionStrings:RulesDb" "Server=localhost,14333;Database=CitSyncRules;User Id=sa;Password=<your password>;TrustServerCertificate=True" --project src/CitSync.Rules.Api
   ```

3. **Apply the migrations and run the API:**

   ```bash
   dotnet ef database update -p src/CitSync.Rules.Infrastructure -s src/CitSync.Rules.Api
   dotnet run --project src/CitSync.Rules.Api
   ```

   Swagger UI: `http://localhost:5212/swagger` · Health: `http://localhost:5212/health`.
   You can send sample requests with `src/CitSync.Rules.Api/CitSync.Rules.Api.http`.

4. **Run the tests.** Docker must be running, because the integration tests start their own
   throwaway SQL Server container:

   ```bash
   dotnet test
   ```

## Endpoints available today (v1)

| Method | Route | Notes |
|---|---|---|
| GET | `/api/v1/resources?page=&pageSize=` | paginated |
| GET | `/api/v1/resources/{id}` | 404 ProblemDetails if missing |
| POST | `/api/v1/resources` | 400 validation, 409 duplicate name |
| GET | `/api/v1/reservations?resourceId=&status=&from=&to=&page=&pageSize=` | paginated, window filter |
| GET | `/api/v1/reservations/{id}` | times in UTC and America/Mexico_City |
| POST | `/api/v1/reservations` | checks the resource exists, is active, and has enough capacity |
| POST | `/api/v1/reservations/{id}/approve` \| `/reject` \| `/cancel` | 409 on an invalid state transition |
| GET | `/health` | includes the SQL Server check |

## Phases

| # | Phase | Status |
|---|---|---|
| 1 | Solution skeleton, Docker SQL Server, DbContext, first migration, health check, Swagger, CI | ✅ Done |
| 2 | Resources + Reservations CRUD with FluentValidation and ProblemDetails | ✅ Done |
| 3 | RuleSets with versioning + approval state machine + audit interceptor + optimistic concurrency | ⏳ Next |
| 4 | Rule evaluation engine (strategy pattern) + `POST /api/v1/rules/evaluate` | — |
| 5 | Full unit + integration coverage of every rule type and state transition | — |
| 6 | Serilog, OpenTelemetry, query-tuning doc, runbook, ADRs, AI-usage section | — |
| 7 | CIT-Sync integration: BookingModal calls `/evaluate`, AdminPanel approves/rejects through the API | — |

Some small pieces are marked `// TODO(Marcus):` in the code on purpose. I implement those by hand
as interview practice, and the related tests stay `Skip`ped until I do.
