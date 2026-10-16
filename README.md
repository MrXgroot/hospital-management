# City Care HMS

Hospital management system: patients, doctors, appointments, consultations, prescriptions, demo billing and a
dashboard. ASP.NET Core (.NET 10) + MySQL 8 backend, React + TypeScript frontend.

## Run it locally

Prerequisites: .NET 10 SDK, Node.js 20+, MySQL 8.

1. **Database**: create a database and user, then store the connection string as a user secret:
   ```powershell
   cd backend
   dotnet user-secrets set "ConnectionStrings:HmsDb" "Server=localhost;Port=3306;Database=hms_dev;User=hms_app;Password=<password>" --project src/HMS.Api
   ```
2. **API** (applies migrations and seeds demo data on first start in Development):
   ```powershell
   cd backend
   dotnet run --project src/HMS.Api --launch-profile https
   ```
   Swagger UI: https://localhost:7001/swagger
3. **Frontend**:
   ```powershell
   cd frontend
   npm install
   npm run dev
   ```
   Open http://localhost:5173

## Demo logins

| Username | Password | Role |
|---|---|---|
| `admin` | `Admin@1234` | Admin |
| `reception` | `Demo@1234` | Receptionist |
| `nurse` | `Demo@1234` | Nurse |
| `dr.sharma`, `dr.nair`, `dr.verma`, `dr.iyer` | `Demo@1234` | Doctor |

These are development-only credentials created by the seeder. Change them before any real use.

## Tests

```powershell
cd backend;  dotnet test tests/HMS.UnitTests
# Integration tests need a real MySQL: either Docker running, or
$env:HMS_TEST_CONNECTION = "Server=localhost;Port=3306;User=hms_app;Password=<password>"; dotnet test tests/HMS.IntegrationTests
cd frontend; npm run typecheck; npm run lint; npm run test
```

See [CLAUDE.md](CLAUDE.md) and [docs/](docs/) for architecture, database design, API contract and business rules.
