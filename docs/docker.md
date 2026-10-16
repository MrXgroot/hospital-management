# Docker Guide — Hospital Management System Backend

This guide describes how to build, run, and validate the Docker container for the ASP.NET Core backend.

---

## 1. Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine (Linux containers enabled).
- A running MySQL 8 database instance accessible from Docker.

---

## 2. Docker Architecture

- **Multi-Stage Build**:
  - **Build stage**: Uses `mcr.microsoft.com/dotnet/sdk:10.0` to restore dependencies and publish the `HMS.Api` project in `Release` mode with `--no-restore`.
  - **Runtime stage**: Uses `mcr.microsoft.com/dotnet/aspnet:10.0` for a minimal, production-oriented container image.
- **Security**:
  - Runs as the unprivileged, non-root user `app` (UID 1654) built into Microsoft's official .NET runtime images.
  - No database credentials, passwords, or secrets are baked into the image.
- **Port Binding**:
  - Dynamically binds to the `PORT` environment variable (e.g. provided by cloud hosting like Render), defaulting to `8080` if `PORT` is not set.

---

## 3. How to Build the Image

### Option A: From the `backend/` directory (Recommended)
```bash
cd backend
docker build -t hms-api .
```

### Option B: From the repository root
```bash
docker build -t hms-api -f backend/Dockerfile backend
```

> **Note**: The Docker build context MUST be the `backend` folder so that `HospitalManagement.sln`, `Directory.Build.props`, `nuget.config`, and all project references are included.

---

## 4. How to Run the Container

### Important: Database Host Resolution
Inside a Docker container, `localhost` refers to the **container itself**, not your host machine.
- To connect to a MySQL server running on your **host machine** (Windows or macOS):
  Use `host.docker.internal` instead of `localhost` (e.g., `Server=host.docker.internal;Port=3306;...`).
- To connect to a MySQL server on the **local network or cloud**:
  Use the external IP or domain name of the MySQL server (e.g., `Server=mysql.example.com;Port=3306;...`).

### Command to Run
```bash
docker run --rm -d \
  -p 8080:8080 \
  --name hms-api \
  -e ConnectionStrings__HmsDb="Server=host.docker.internal;Port=3306;Database=hms_dev;User=hms_app;Password=YourPassword123!;SslMode=Preferred;" \
  -e Jwt__SigningKey="a_secure_production_key_with_at_least_32_characters_12345" \
  -e ASPNETCORE_ENVIRONMENT="Production" \
  hms-api:latest
```

### Running on a Custom Port (e.g., matching Render's `PORT`)
```bash
docker run --rm -d \
  -p 10000:10000 \
  --name hms-api \
  -e PORT=10000 \
  -e ConnectionStrings__HmsDb="Server=host.docker.internal;Port=3306;Database=hms_dev;User=hms_app;Password=YourPassword123!;SslMode=Preferred;" \
  -e Jwt__SigningKey="a_secure_production_key_with_at_least_32_characters_12345" \
  hms-api:latest
```

---

## 5. Verification & Health Check

### Check Container Logs
```bash
docker logs hms-api
```
Expected output:
```text
Now listening on: http://0.0.0.0:8080
Application started. Press Ctrl+C to shut down.
Hosting environment: Production
```

### Call the Health Endpoint
```bash
curl http://localhost:8080/health
```
Expected response:
```json
{"status":"ok"}
```

### Stop the Container
```bash
docker stop hms-api
```
