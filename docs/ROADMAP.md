# Roadmap and Progress

Tick items as they are completed (Claude updates this at the end of each module).

## Phase 0: Setup
- [x] Backend solution + 4 projects + test projects, `Directory.Build.props` (warnings as errors)
- [x] MySQL connection, `HmsDbContext`, snake_case naming, audit interceptor, `IClock`, `ICurrentUser`
- [x] Exception middleware → ProblemDetails, Serilog, OpenAPI + Swagger UI, CORS, login rate limit
- [x] Frontend Vite app, MUI theme, router, TanStack Query, axios client, layout + nav
- [x] Dev seed data (`DbSeeder` with `Seed:DemoData`, replacing the planned SQL file)

## Phase 1: Auth and users
- [x] Users, BCrypt, JWT + rotating refresh tokens, role-based authorization
- [x] Login page, AuthProvider, RequireRole, admin user management

## Phase 2: Doctor Management
- [x] Departments, specializations, medicines (masters)
- [x] Doctor CRUD, doctor code, active/inactive with future-appointment check
- [x] Weekly schedule editor (overlap validation), leaves
- [x] Slot generation endpoint

## Phase 3: Patient Management
- [x] Registration with MRN sequence, duplicate warning
- [x] Search (MRN / phone / name), detail, update with audit
- [x] Allergies, chronic conditions, visit history tab

## Phase 4: Appointment Management
- [x] Booking page, slot picker, walk-ins, follow-ups
- [x] Double-booking prevention (service + unique index) + concurrency integration test
- [x] Confirm, check-in (token), cancel, reschedule, no-show, status history
- [x] Daily queue screen (30 s refresh)

## Phase 5: Consultation and Medical Records
- [x] Start consultation from queue (or walk-in by patient), Draft/Finalise
- [x] Symptoms, diagnosis, notes, vitals (range warnings, BMI)
- [x] Revisions + amendments with reason, History tab with field diff
- [x] Previous consultations panel

## Phase 6: Prescription Management
- [x] Prescription editor (medicine search + free text), Rx number
- [x] Finalise, amendments, allergy warning
- [x] Print view (A4), follow-up booking shortcut

## Phase 7: Billing (demo)
- [x] Auto invoice on check-in, "Mark as paid" (method + reference), cancel

## Phase 8: Dashboard
- [x] Summary endpoint, KPI cards, waiting list, available doctors, quick links, doctor-scoped view

## Next candidates (not started)
- [ ] Partial payments, discounts and extra invoice items (deliberately left out of the demo billing)
- [ ] Server-side PDF for prescriptions
- [ ] Appointment calendar/week view
- [ ] Patient vitals trend chart (`GET /patients/{id}/vitals` already exists)
- [ ] Docker Compose for API + MySQL; CI pipeline running both test suites
- [ ] Out-of-scope modules from FEATURES.md (pharmacy, lab, IPD, insurance, notifications)
