# Docker Setup

This repository does not include a `Dockerfile` for the services, but it **does** use Docker indirectly via **.NET Aspire** to provision infrastructure dependencies (notably SQL Server) for local development.

## Option A (Recommended): Use Aspire AppHost

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

## Notes And Gotchas

- **Production safety:** In non-Development environments, the app is configured to fail fast if `ConnectionStrings:DefaultConnection` is missing.
- **InMemory is not SQL Server:** EF Core InMemory is useful for unit tests but does not match SQL Server behavior; prefer SQL Server (Aspire/Docker/LocalDB) for integration testing.
- **Secrets:** Do not hardcode SA passwords in source control; prefer user-secrets or environment variables.
