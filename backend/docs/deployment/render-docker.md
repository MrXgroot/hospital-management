# Deploying HMS Backend to Render with Docker

This guide explains how to deploy the Hospital Management System (HMS) ASP.NET Core backend to [Render](https://render.com) using Docker.

---

## 1. Overview & Service Settings

When creating a new **Web Service** in Render:

| Setting | Value | Notes |
|---|---|---|
| **Language / Environment** | `Docker` | Multi-stage build defined in `Dockerfile` |
| **Root Directory** | `backend` | If setting the root directory to `backend` in Render |
| **Docker Build Context** | `.` (or `backend` from repo root) | Contains `HospitalManagement.sln`, `nuget.config`, projects |
| **Dockerfile Path** | `Dockerfile` | Multi-stage build using .NET 10 |
| **Health Check Path** | `/health` | Responds with HTTP 200 `{"status":"ok"}` |
| **Instance Type** | Free / Starter or higher | Requires at least 512 MB RAM |

---

## 2. Environment Variables

Configure the following environment variables in the Render Dashboard (**Environment** tab of your Web Service):

### Required Environment Variables

| Variable Name | Example / Format | Description |
|---|---|---|
| `ConnectionStrings__HmsDb` | `Server=<host>;Port=<port>;Database=<db>;User=<user>;Password=<secret>;SslMode=Required;` | MySQL connection string. Stored securely as an environment variable in Render. |
| `Jwt__SigningKey` | `<random-32+-character-string>` | Cryptographic secret for signing JWT tokens. Must be at least 32 characters long. In Production, startup will fail if this starts with `DEV-ONLY`. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runs the API in production mode. |
| `Cors__AllowedOrigins__0` | `https://your-frontend.onrender.com` | Allowed origin for frontend requests. If multiple origins are needed, use `Cors__AllowedOrigins__1`, etc. |

### Optional Configuration Variables

| Variable Name | Default | Description |
|---|---|---|
| `PORT` | Set by Render (e.g. `10000`) | Render automatically sets this. The container entrypoint automatically maps this to ASP.NET Core's listening URL `http://0.0.0.0:$PORT`. |
| `Seed__MigrateOnStartup` | `false` | Set to `true` if you want EF Core migrations applied during startup. Keep `false` if migrations are applied separately via CI/CD or CLI scripts. |
| `Seed__AdminPassword` | None | Required only if `users` table is empty and initial `admin` user is being created. |
| `Seed__DemoData` | `false` | Must remain `false` in production. Demo data is intended for local development only. |

> [!IMPORTANT]
> Never commit real database credentials or JWT keys to Git or hardcode them in `appsettings.json`. Use Render's Environment Variables or Secret Files.

---

## 3. Database Configuration (MySQL)

The backend uses Entity Framework Core with MySQL (`Pomelo.EntityFrameworkCore.MySql 9.0.0` pinned to server version `8.0.36`).

### Connecting to an External MySQL Server
Render does not host managed MySQL directly. You should use a cloud MySQL provider (such as Aiven, PlanetScale, AWS RDS, DigitalOcean, or Azure Database for MySQL).

1. In your MySQL provider, create your database and database user with permissions for `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE`, `INDEX`, `ALTER`.
2. Ensure your MySQL server allows incoming connections from Render (or public IP allowlist).
3. In Render, add the environment variable `ConnectionStrings__HmsDb`:
   ```text
   Server=mysql-service.example.com;Port=3306;Database=hms_prod;User=hms_user;Password=YourSecurePassword123!;SslMode=Required;
   ```
4. ASP.NET Core automatically maps double underscores (`__`) in environment variables to configuration sections (`ConnectionStrings:HmsDb`), which is read by `builder.Services.AddInfrastructure(...)`.

---

## 4. Port Configuration

- Render dynamically assigns an external port and sets the `PORT` environment variable (typically `10000`).
- The `Dockerfile` includes an entrypoint script that inspects `$PORT`:
  ```sh
  if [ -n "$PORT" ]; then
    export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
  elif [ -z "$ASPNETCORE_URLS" ]; then
    export ASPNETCORE_URLS="http://0.0.0.0:8080"
  fi
  exec dotnet HMS.Api.dll "$@"
  ```
- This binds Kestrel to all network interfaces (`0.0.0.0`) on the port specified by Render without modifying application source code.

---

## 5. Health Check Configuration

- Path: `/health`
- Method: `GET`
- Expected Response: `HTTP 200 OK` with JSON body `{"status":"ok"}`
- Set **Health Check Path** in Render Service Settings to `/health`. Render will periodically send HTTP requests to this endpoint to determine service health and facilitate zero-downtime rolling deploys.

---

## 6. Frontend Integration & CORS

The React frontend and ASP.NET Core backend are deployed as separate services.

### Frontend Configuration
In the React frontend project (e.g., deployed on Render Static Site or Vercel):
- Set the environment variable:
  ```text
  VITE_API_BASE_URL=https://<your-backend-name>.onrender.com
  ```

### Backend CORS Configuration
In the backend Render service:
- Set:
  ```text
  Cors__AllowedOrigins__0=https://<your-frontend-name>.onrender.com
  ```
- If testing locally against the deployed backend or using custom domains, add additional origins:
  ```text
  Cors__AllowedOrigins__1=https://app.yourhospitaldomain.com
  ```

---

## 7. Verification Steps

1. **Deploy the Service**: Trigger a manual deploy or push to your tracked branch.
2. **Review Logs**: In Render's **Logs** view, verify that:
   - The multi-stage Docker build succeeds.
   - The application starts with `Hosting environment: Production`.
   - Kestrel reports `Now listening on: http://0.0.0.0:<PORT>`.
3. **Verify Health Endpoint**:
   ```bash
   curl -i https://<your-backend-name>.onrender.com/health
   ```
   Should return:
   ```http
   HTTP/1.1 200 OK
   Content-Type: application/json; charset=utf-8

   {"status":"ok"}
   ```
4. **Verify Database Connectivity**:
   Send an authentication attempt:
   ```bash
   curl -i -X POST https://<your-backend-name>.onrender.com/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"TestPassword"}'
   ```
   A response of `401 Unauthorized` (or `200 OK` with token if credentials match) confirms that the API is actively communicating with the MySQL database.
