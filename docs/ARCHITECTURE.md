# Architecture

## Overview

```
React SPA (Vite)  ──HTTPS/JSON──►  ASP.NET Core Web API  ──EF Core──►  MySQL 8
  TanStack Query                     JWT auth + role policies
  React Router                       Controllers → Services → DbContext
```

Single deployable API plus a static SPA. No microservices.

## Backend: layered (Clean Architecture, lightweight)

| Project | Responsibility | May reference |
|---|---|---|
| `HMS.Domain` | Entities, enums, domain rules (e.g. appointment status transitions), and the typed exceptions they throw. No EF, no ASP.NET. | nothing |
| `HMS.Application` | Use-case services (`IPatientService`, ...), DTOs, FluentValidation validators, mapping, interfaces (`IAppDbContext`, `IClock`, `ICurrentUser`, `ISequenceGenerator`, `ITokenService`, `IPasswordHasher`) | Domain |
| `HMS.Infrastructure` | `HmsDbContext`, entity configurations (`IEntityTypeConfiguration<T>`), migrations, sequence generator, audit/revision interceptors | Application, Domain |
| `HMS.Api` | Controllers, auth, middleware (exception → ProblemDetails), Swagger, DI composition root | Application, Infrastructure |

**Decisions**
- **No generic repository.** Services use `IAppDbContext` (which exposes `DbSet<T>`s) directly. EF Core already
  is a repository/unit of work.
- **No MediatR.** Plain service classes per module (`PatientService`, `AppointmentService`, ...). Easy to read
  and debug.
- **DTOs at the API boundary.** Never return EF entities from controllers.
- **Validation:** FluentValidation for request shape. Domain/service checks for business rules (they return
  `422`).
- **Errors:** services and entities throw typed exceptions from `HMS.Domain.Common` (`NotFoundException`,
  `ConflictException`, `BusinessRuleException`, `ForbiddenException`, `RequestValidationException`).
  Middleware maps them to RFC 7807 ProblemDetails with a stable `code`. Extra data goes in `.With(key, value)`.
- **Time:** inject `IClock` (wraps `TimeProvider`). Never call `DateTime.Now` or `DateTime.UtcNow` directly,
  so tests can control time.
- **Current user:** `ICurrentUser` (user id, role, doctor id), read from JWT claims.
- **Auditing:** an EF `SaveChangesInterceptor` sets `created_at/by` and `updated_at/by`, and writes `audit_logs`
  rows for patients and doctors. Clinical revisions (`clinical_record_revisions`) are written explicitly by
  the consultation and prescription services, because they need a reason and a full snapshot.
- **Concurrency:** `row_version` (`INT`, incremented in the interceptor) as an EF concurrency token on
  appointments, consultations, prescriptions, and invoices. Conflicts → `409 CONCURRENCY_CONFLICT`.

### Request flow example: book appointment

```
POST /api/appointments
 → AppointmentsController.Create (policy: Receptionist,Admin)
 → CreateAppointmentValidator (shape)
 → AppointmentService.CreateAsync
     begin transaction
     load doctor (active?), schedule, leave
     verify slot is in schedule and free; patient has no same-doctor same-day booking
     insert appointment (+ status history row)
     commit  ── duplicate key on UX_appointments_active_slot → ConflictException(SLOT_UNAVAILABLE)
 → 201 Created + AppointmentDto
```

## Frontend

- **Vite + React 19 + TypeScript (strict).**
- **Routing:** React Router v7 (data/declarative mode), with route guards by role.
- **Server state:** TanStack Query. No Redux. Local UI state uses `useState`, and the auth session lives in
  a small Context.
- **HTTP:** Axios instance in `src/api/client.ts`, with an interceptor that adds the bearer token and handles
  401 → logout. Each module has `src/api/<module>.ts` exporting typed functions.
- **Forms:** React Hook Form + Zod schemas (mirror the backend validators).
- **UI kit:** MUI 7. No MUI X: native date/time inputs and MUI `Table` keep the dependency surface small.
- **Dates:** dayjs.
- **Feature folders:** `src/features/<module>/` (pages + module components/schemas). API functions live in
  `src/api/endpoints.ts`, query keys in `src/api/queryKeys.ts`, DTO types in `src/types/api.ts`.

## Approved dependencies

Backend: `Microsoft.EntityFrameworkCore(.Relational/.Design)` **9.0.x**, `Pomelo.EntityFrameworkCore.MySql` **9.0.0**
(major must match EF Core; Pomelo has no EF Core 10 release yet, so the app targets .NET 10 with EF Core 9), `EFCore.NamingConventions`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `FluentValidation`, `Serilog.AspNetCore`,
`Microsoft.AspNetCore.OpenApi` + `Swashbuckle.AspNetCore.SwaggerUI` (UI only), `BCrypt.Net-Next`,
`System.IdentityModel.Tokens.Jwt`. Tests: `xunit`, `Shouldly` (FluentAssertions v8 needs a commercial licence),
`NSubstitute`, `Testcontainers.MySql`, `Microsoft.AspNetCore.Mvc.Testing`.

Frontend: `react-router` 7, `@tanstack/react-query` 5, `axios`, `react-hook-form`, `zod` 4, `@hookform/resolvers`,
`@mui/material` 7 + `@mui/icons-material` + emotion, `dayjs`. Dev: `vite` 7, `typescript` 5.9, `vitest` 3,
`@testing-library/*`, `jsdom`, `eslint` 9 + `typescript-eslint`. Newer majors (TS 7, MUI 9, Router 8) are
deliberately not used yet.

Ask before adding anything else.

## Security

- JWT access token (15 min) plus a refresh token (7 days, stored hashed in `refresh_tokens`, rotated on use).
- Passwords: BCrypt (work factor 12).
- Role policies on every controller action. Doctors are additionally restricted to their own consultations
  for writes (checked in the service).
- CORS: allow only the configured frontend origin.
- Serilog: log request path, status, duration, and user id. **Never log request bodies** (they contain PHI).
- Rate-limit `/api/auth/login` and `/refresh` (`RateLimit:LoginPerMinute`, default 10 per IP).
