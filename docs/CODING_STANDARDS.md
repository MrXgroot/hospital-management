# Coding Standards

## C# / Backend

**General**
- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  (shared via `Directory.Build.props`).
- File-scoped namespaces. One public type per file. Folder = namespace.
- `async` all the way. Every async method takes a `CancellationToken ct` last and passes it through. Name
  them `XxxAsync`.
- Primary constructors for DI in services and controllers.
- Use `record` for DTOs and requests: `public sealed record CreatePatientRequest(...)`.
- Naming: `PascalCase` types and members, `_camelCase` private fields, `camelCase` locals and parameters.
- No magic strings for roles or policies. Use `Roles.Admin` and `Policies.CanManagePatients` constants.

**Folder pattern per module** (example: Patients)
```
HMS.Domain/Patients/Patient.cs, PatientAllergy.cs, Gender.cs
HMS.Application/Patients/
    IPatientService.cs, PatientService.cs
    Dtos/PatientDto.cs, PatientListItemDto.cs
    Requests/CreatePatientRequest.cs, UpdatePatientRequest.cs
    Validators/CreatePatientRequestValidator.cs
    PatientMappings.cs             // manual extension methods: ToDto(), no AutoMapper
HMS.Infrastructure/Persistence/Configurations/PatientConfiguration.cs
HMS.Api/Controllers/PatientsController.cs
```

**Controllers**
- Thin: validate → call service → map result to an HTTP response. No business logic and no DbContext.
- `[ApiController]`, `[Route("api/[controller]")]`, an explicit `[Authorize(Policy = ...)]` on every action
  (or on the class, with overrides).
- Return `ActionResult<T>`. Use `CreatedAtAction` for POST.

**EF Core**
- Use `AsNoTracking()` for reads. Project to DTOs with `.Select(...)` for lists, rather than loading
  entities and mapping.
- Use explicit `Include` only when you need the entity graph for a write.
- No lazy loading.
- Wrap multi-step writes in `await using var tx = await db.Database.BeginTransactionAsync(ct);`.
- Configure every entity in its own `IEntityTypeConfiguration<T>`. No data annotations on domain entities.

**Domain**
- Entities expose behaviour methods (`appointment.Cancel(reason, clock)`, `consultation.Finalise(...)`) and
  keep setters `private` or `internal` where practical. Status transitions live in the domain.

**Testing**
- Unit tests (`HMS.UnitTests`): domain rules (status transitions, slot generation, MRN format, BMI) and
  services with an in-memory SQLite or a substitute context. Name tests `Method_Scenario_ExpectedResult`.
- Integration tests (`HMS.IntegrationTests`): `WebApplicationFactory` + real MySQL, either a throwaway database
  on a local server (`HMS_TEST_CONNECTION`) or **Testcontainers** (Docker), so the generated column and unique
  index behave correctly. Assertions use Shouldly. Required for: double-booking prevention
  (including two concurrent requests), MRN uniqueness under concurrency, amendment history, and auth policies.
- Every bug fix comes with a test that fails before the fix.

## React / Frontend

- TypeScript `strict: true`. No `any`. Use `unknown` and narrow it.
- Function components only. Named exports, except route pages, which may use default exports for lazy loading.
- File names: components `PascalCase.tsx`, hooks `useXxx.ts`, other files `camelCase.ts`.
- **Data fetching only through TanStack Query** (`useQuery`/`useMutation` calling `src/api/endpoints.ts`).
  Components never call axios directly.
- Query keys come from `src/api/queryKeys.ts`: `keys.patients.all`, `keys.patients.detail(id)`, ...
  Invalidate precisely after mutations.
- Forms: React Hook Form + a Zod schema next to the page (e.g. `features/patients/patientSchema.ts`). Show server `errors` from
  ProblemDetails on the matching fields.
- Handle ProblemDetails `code`s centrally (`src/api/errors.ts`). Show friendly messages: e.g.
  `SLOT_UNAVAILABLE` → "That slot was just taken. Please pick another one" (and refetch the slots).
- Route guards: `<RequireRole roles={['Admin','Receptionist']}>`. Hide the nav items a role can't use. The
  backend is still the real security boundary.
- Dates: always format through `src/utils/date.ts` helpers (dayjs). Send `YYYY-MM-DD` and `HH:mm` to the API.
- Print views live under `/print/...` routes with a minimal layout and `@media print` styles.
- Accessibility: every input has a label, buttons have text or `aria-label`, and forms are keyboard-navigable.
- Tests: Vitest + Testing Library; mock modules with `vi.mock` (no MSW). Cover form schemas, error mapping and
  the role-based nav.
- Never store PHI in `localStorage`. Only the auth tokens go there (or use memory + refresh cookie in v2).

## Git

- Branches: `feature/<module>-<short-desc>`, `fix/<short-desc>`.
- Commits: Conventional Commits (`feat(appointments): prevent double booking`).
- PRs: one module or slice per PR, and docs updated in the same PR.
