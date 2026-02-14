# Docker Setup

This repository includes container assets for deployable services and supports Docker-backed local development.

## Option A (Recommended for local development): Use Aspire AppHost

The Aspire host is `src/Aonik.AppHost`, and its orchestration is defined in `src/Aonik.AppHost/AppHost.cs`:

- Starts a SQL Server resource named `sql`
- Creates a database named `aonikdb`
- Runs the API and Worker and references the SQL Server resource
- Uses `ContainerLifetime.Persistent` so the SQL container and its data persist between runs

### Prerequisites

- Docker Desktop (or another Docker engine supported by Aspire)
- .NET SDK (`net10.0`)

### Run

```bash
dotnet run --project src/Aonik.AppHost
```

What you should see:

- Aspire will spin up a SQL Server container.
- The API and Worker run as Aspire-managed projects.

### Resetting the database

Because the SQL Server container lifetime is persistent, the DB will be retained between runs. To reset the database, remove the corresponding container/volume via Docker.

## Option B: Run SQL Server In Docker Manually

If you prefer not to use Aspire, you can run SQL Server yourself and point the API at it.

### Start SQL Server container

Example (SQL Server 2022):

```bash
docker run --name aonik-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=Your_strong_Password123!" \
  -p 1433:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### Configure AONIK to use it

Set the connection string for the API:

- Config key: `ConnectionStrings:DefaultConnection`
- Environment variable form: `ConnectionStrings__DefaultConnection`

Example (PowerShell):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=AonikDb;User Id=sa;Password=Your_strong_Password123!;TrustServerCertificate=True;"
dotnet run --project src/Aonik.Api
```

Then apply migrations:

```bash
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

## Option C: Build and run deployable service containers

AONIK now includes production-ready container definitions:

- `docker/api.Dockerfile`
- `docker/worker.Dockerfile`
- `docker/compose.yml`
- `.dockerignore`

### Build service images

From repository root:

```bash
docker build -f docker/api.Dockerfile -t aonik-api:local .
docker build -f docker/worker.Dockerfile -t aonik-worker:local .
```

### Run full stack with Docker Compose

From `docker/` directory:

```bash
docker compose up --build
```

Services:

- API: `http://localhost:8080`
- SQL Server: `localhost:1433`
- Worker: background process (no exposed HTTP port)

To stop and remove containers:

```bash
docker compose down
```

To also remove SQL data volume:

```bash
docker compose down -v
```

## Modern containerisation strategy (recommended)

For production and CI/CD, use a **two-track strategy**:

1. **Aspire for developer orchestration**
2. **Hardened OCI images for deployable services** (`Aonik.Api`, `Aonik.Worker`)

### Build and runtime hardening

- Use multi-stage Dockerfiles per service.
- Build with SDK images, run on minimal runtime images.
- Run containers as non-root users.
- Keep build contexts small with `.dockerignore`.

### CI/CD and supply-chain controls

- Build via `docker buildx build` with layer caching.
- Publish immutable tags (`git-sha`) and promotion tags (`main`, `release-x.y`).
- Generate SBOMs and run vulnerability scans before deploy.
- Sign images and enforce signature verification in runtime environments.

### Runtime operations

- Keep secrets in managed secret stores, not in images.
- Expose health checks and wire orchestrator probes.
- Emit logs/metrics/traces using OpenTelemetry-compatible pipelines.
- Scale API and Worker independently.

### Alignment with AONIK architecture principles

- Container boundaries should follow deployable responsibilities, not financial invariants.
- Financial correctness remains enforced by ledger integrity, application services, policy controls, and auditable AI/agent execution.
- Orders remain business intent; payments execute intent; ledger proves state.

## Notes And Gotchas

- **Production safety:** In non-Development environments, the app is configured to fail fast if `ConnectionStrings:DefaultConnection` is missing.
- **InMemory is not SQL Server:** EF Core InMemory is useful for unit tests but does not match SQL Server behavior; prefer SQL Server (Aspire/Docker/LocalDB) for integration testing.
- **Secrets:** Do not hardcode SA passwords in source control; prefer user-secrets or environment variables.
