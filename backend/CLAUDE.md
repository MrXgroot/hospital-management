# Backend (ASP.NET Core / EF Core / MySQL)

Read the root [CLAUDE.md](../CLAUDE.md) first. Specs: [DATABASE.md](../docs/DATABASE.md), [API.md](../docs/API.md),
[CODING_STANDARDS.md](../docs/CODING_STANDARDS.md).

## Commands (from `backend/`)
```bash
dotnet tool restore
dotnet build                     # TreatWarningsAsErrors is on
dotnet test tests/HMS.UnitTests
dotnet run --project src/HMS.Api --launch-profile https
dotnet ef migrations add <Name> -p src/HMS.Infrastructure -s src/HMS.Api -o Persistence/Migrations
dotnet ef migrations script -p src/HMS.Infrastructure -s src/HMS.Api      # review the SQL
```
`dotnet ef` uses `DesignTimeDbContextFactory` (no DB connection needed to add migrations).

## Where things are
- `src/HMS.Api/Program.cs`: DI, JWT, fallback auth policy, CORS, rate limit, Serilog, OpenAPI, seeding
- `src/HMS.Api/Infrastructure/ApiInfrastructure.cs`: `HttpCurrentUser`, `ValidationFilter` (FluentValidation →
  400), `ExceptionHandlingMiddleware` (exception → ProblemDetails + `code`), `TimeOnly` "HH:mm" converter, `Roles`
- `src/HMS.Api/Controllers/`: thin controllers; role lists come from `Roles` constants
- `src/HMS.Application/<Module>/`: `*Contracts.cs` (DTOs, requests, validators) + `*Service.cs`
- `src/HMS.Application/Common/`: `IAppDbContext`, `IClock`, `ICurrentUser`, `ISequenceGenerator`, paging, `DbErrors`
- `src/HMS.Application/Clinical/ClinicalRevisionWriter.cs`: append-only clinical history + field diffs
- `src/HMS.Domain/Common/Exceptions.cs`: `NotFound`/`Conflict`/`BusinessRule`/`Forbidden`/`RequestValidation`
  exceptions (in Domain so entities can throw them). Status mapping: 404/409/422/403/400.
- `src/HMS.Infrastructure/Persistence/`: `HmsDbContext`, `Configurations/`, `Migrations/`, `AuditInterceptor`,
  `Seed/DbSeeder.cs`
- `src/HMS.Infrastructure/Services/InfrastructureServices.cs`: `SystemClock`, `SequenceGenerator`, BCrypt, JWT

## Checklist for every new endpoint
- [ ] Request record + FluentValidation validator (validators may inject `IClock`)
- [ ] Service method with `CancellationToken`, using `IClock` and `ICurrentUser` (never `DateTime.Now`)
- [ ] Business-rule violations throw typed exceptions with a stable `code` (listed in API.md)
- [ ] `[Authorize(Roles = Roles.X)]` matches the roles column in API.md
- [ ] Ownership check for doctor-only writes (`currentUser.EnsureOwnsClinicalRecord`)
- [ ] Reads use `AsNoTracking()` + `Select` projection
- [ ] No PHI in log messages
- [ ] Unit tests for rules. Integration test if it touches concurrency, unique indexes, or auth
- [ ] API.md / DATABASE.md updated if the contract or schema changed

## Pomelo / MySQL notes
- `UseMySql(cs, new MySqlServerVersion(8.0.36))` + `UseSnakeCaseNamingConvention()`. **No**
  `EnableRetryOnFailure`: services use explicit `BeginTransactionAsync`, which retrying strategies don't allow.
- Sequence numbers (MRN, doctor code, Rx, invoice) come from `ISequenceGenerator`, which **must** run inside a
  transaction (upsert + read on `id_sequences`; the row lock serialises callers).
- Duplicate keys: `DbErrors.IsUniqueViolation(ex, "<index name>")` (MySQL 1062 names the index).
- Generated columns (`appointments.active_slot`, `invoices.balance_due`) are mapped with
  `HasComputedColumnSql(..., stored: true)` and private setters, so EF never writes them.
- `DateOnly`/`TimeOnly` map to `DATE`/`TIME`.
- `Program` reads config while building, so integration tests pass settings as **environment variables**
  (see `HmsApiFactory`), not `ConfigureAppConfiguration`.
