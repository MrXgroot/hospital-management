# Hospital Management System (HMS)

Web application for a small hospital/clinic: patient registration, doctors, appointments,
consultations (medical records), prescriptions, demo billing, and a dashboard. All v1 modules are built
(see [docs/ROADMAP.md](docs/ROADMAP.md)).

- **Backend:** C# / ASP.NET Core Web API on **.NET 10**, **EF Core 9 + Pomelo 9** (Pomelo has no EF Core 10
  release yet; EF Core 9 runs fine on .NET 10), MySQL 8
- **Frontend:** React 19 + TypeScript 5.9 + Vite 7, MUI 7, TanStack Query 5, React Router 7, React Hook Form + Zod 4
- **Database:** MySQL 8.0+ (InnoDB, utf8mb4). Local dev uses the `MySQL80` Windows service on port 3306.
- **Locale:** INR (₹), time zone `Asia/Kolkata`

## Read these before working on a feature

| Doc | When to read it |
|---|---|
| [docs/FEATURES.md](docs/FEATURES.md) | Requirements and business rules for every module. **The source of truth for behaviour.** |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Solution layout, layers, request flow, cross-cutting concerns |
| [docs/DATABASE.md](docs/DATABASE.md) | Tables, columns, constraints, indexes, migration rules |
| [docs/API.md](docs/API.md) | REST endpoint contract, response/error shapes |
| [docs/CODING_STANDARDS.md](docs/CODING_STANDARDS.md) | C# and React conventions, testing rules |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Build phases and progress checklist (update when a module is done) |
| [backend/CLAUDE.md](backend/CLAUDE.md) | Backend-specific instructions (loaded when working in `backend/`) |
| [frontend/CLAUDE.md](frontend/CLAUDE.md) | Frontend-specific instructions (loaded when working in `frontend/`) |

If code and docs disagree, stop and ask which one is correct. Don't silently "fix" either.
When you change a table, endpoint, or business rule, update the matching doc **in the same change**.

## Repository layout

```
/
├── CLAUDE.md, README.md
├── docs/                       # Specs (features, db, api, architecture, standards, roadmap)
├── backend/
│   ├── HospitalManagement.sln
│   ├── nuget.config            # nuget.org source (the machine-level NuGet config has none)
│   ├── dotnet-tools.json       # local dotnet-ef 9.0.21 → `dotnet tool restore`
│   ├── src/
│   │   ├── HMS.Api/            # Controllers, middleware, Program.cs, auth
│   │   ├── HMS.Application/    # Services, DTOs, validators, interfaces (one folder per module)
│   │   ├── HMS.Domain/         # Entities, enums, domain rules, exception types (no dependencies)
│   │   └── HMS.Infrastructure/ # DbContext, configurations, migrations, seeder, JWT, sequences
│   └── tests/
│       ├── HMS.UnitTests/      # Domain rules (no DB)
│       └── HMS.IntegrationTests/  # Real MySQL: local server or Testcontainers
└── frontend/
    ├── src/
    │   ├── api/                # client.ts (axios + refresh), endpoints.ts, queryKeys.ts, errors.ts
    │   ├── features/<module>/  # Pages and module components
    │   ├── components/         # Shared UI (layout, common, form helpers, notifications)
    │   ├── auth/ types/ utils/ test/
    │   └── main.tsx, routes.tsx, theme.ts
    └── package.json
```

## Prerequisites (local dev)

- .NET 10 SDK (installed via winget). If `dotnet` isn't on PATH in a shell: `C:\Program Files\dotnet\dotnet.exe`
- Node.js 20+ (24 installed) and npm
- MySQL 8 (`MySQL80` service). Client: `C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe` (not on PATH)
- Docker Desktop is optional (only for running integration tests via Testcontainers)

## Common commands

### Backend (run from `backend/`)
```bash
dotnet tool restore                                   # once: local dotnet-ef
dotnet build
dotnet run --project src/HMS.Api --launch-profile https   # https://localhost:7001 + http://localhost:5001, Swagger at /swagger
dotnet test tests/HMS.UnitTests
dotnet test                                           # all; integration tests need HMS_TEST_CONNECTION or Docker
dotnet ef migrations add <Name> -p src/HMS.Infrastructure -s src/HMS.Api -o Persistence/Migrations
dotnet ef migrations script -p src/HMS.Infrastructure -s src/HMS.Api   # review SQL before applying
```
In Development the API applies migrations and seeds data on startup (`Seed:MigrateOnStartup`).

Integration tests against the local MySQL (they create and drop a throwaway `hms_test_*` database):
```powershell
$env:HMS_TEST_CONNECTION = "Server=localhost;Port=3306;User=hms_app;Password=<from user-secrets>"
dotnet test tests/HMS.IntegrationTests
```

### Frontend (run from `frontend/`)
```bash
npm install
npm run dev          # http://localhost:5173, proxies /api → http://localhost:5001
npm run typecheck
npm run lint
npm run test         # Vitest
npm run build
```

## Configuration

- Connection string `ConnectionStrings:HmsDb` lives in **dotnet user-secrets** for `HMS.Api` (user `hms_app`,
  database `hms_dev`). `dotnet user-secrets list --project src/HMS.Api` shows it.
- `appsettings.Development.json` holds **dev-only** values: a `DEV-ONLY` JWT key (startup refuses it in
  Production) and the demo admin password. Never put real secrets in any appsettings file.
- `Seed:DemoData=true` (Development) seeds demo doctors, staff users, patients and today's appointments.
- Frontend API base URL: `VITE_API_BASE_URL` (default `/api` through the Vite proxy).

Demo logins (Development seed): `admin / Admin@1234`; `reception`, `nurse`, `dr.sharma`, `dr.nair`,
`dr.verma`, `dr.iyer` with `Demo@1234`.

## Non-negotiable rules

1. **Patient data is sensitive (PHI).** Never log patient names, phone numbers, diagnoses, or notes.
   Log IDs only. Never put PHI in URLs except opaque IDs. Request bodies are never logged.
2. **Clinical records are never hard-deleted or edited in place without history.** Every change to a
   consultation, vitals, or prescription writes a revision row via `ClinicalRevisionWriter`
   (see DATABASE.md → `clinical_record_revisions`). Edits after finalising need an amendment reason.
3. **No double booking.** Appointment conflicts are enforced in the service layer **and** by the
   `UX_appointments_active_slot` unique index. Never remove either one.
4. **Every endpoint requires authentication** (fallback policy) and an explicit role list, except
   login/refresh and `/health`.
5. **Money is `decimal` in C#, `DECIMAL(10,2)` in MySQL.** Never use `float` or `double` for money.
6. **Timestamps are stored in UTC** (`DATETIME(6)`). Appointment date and slot times are hospital-local
   `DATE` and `TIME` values. Always go through `IClock` (never `DateTime.Now`/`UtcNow`).
7. Schema changes go through **EF Core migrations only**. Don't hand-edit the database.
8. Use soft deletes (`is_deleted`) for patients and doctors. Records that are referenced
   by appointments or consultations are never deleted.

## Workflow expectations for Claude

- Before implementing a change, re-read the module's section in `docs/FEATURES.md` and its tables in `docs/DATABASE.md`.
- Build a feature as a vertical slice: Domain entity → EF config + migration → Application service + DTOs +
  validator → Controller → tests → `frontend/src/types/api.ts` + `api/endpoints.ts` → pages/components.
  The `/build-module` skill walks through this.
- Run `dotnet build && dotnet test tests/HMS.UnitTests` (backend) and
  `npm run typecheck && npm run lint && npm run test` (frontend) before saying a task is done. Report failures honestly.
- Keep changes scoped to the task. Don't refactor unrelated code without asking.
- Ask before adding a new NuGet or npm dependency that isn't in the approved list in ARCHITECTURE.md.
