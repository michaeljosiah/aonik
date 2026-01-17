# Local Development

This guide covers local development workflows for AONIK, including how the database is configured in different environments.

## Quick Start (Recommended)

Run the whole stack (API + Worker + SQL Server) via .NET Aspire:

```bash
dotnet run --project src/Aonik.AppHost
```

This uses `src/Aonik.AppHost/AppHost.cs` to provision a SQL Server container (via Docker) and wire it into the API and Worker.

## Prerequisites

- .NET SDK (`net10.0`)
- EF Core tooling (for migrations):

```bash
dotnet tool install --global dotnet-ef
```

Choose one database option:

- **Aspire + Docker (recommended):** Docker Desktop (or another Docker engine supported by Aspire)
- **LocalDB (no Docker):** SQL Server LocalDB (typically installed with Visual Studio)

## How The Database Provider Is Selected

The API registers EF Core in `src/Aonik.Infrastructure/DependencyInjection.cs`.

Rules:

- **Testing:** Always uses EF Core InMemory provider.
  - Driven by `ASPNETCORE_ENVIRONMENT=Testing` and `src/Aonik.Api/appsettings.Testing.json`.
- **Development:** Uses SQL Server by default, but can opt into InMemory by setting `UseInMemoryDatabase=true`.
  - If no connection string is configured, Development falls back to LocalDB.
- **Production (and other non-Development environments):** Uses SQL Server and **fails fast** if `ConnectionStrings:DefaultConnection` is missing.

## Running With Aspire (Docker)

Aspire is configured in `src/Aonik.AppHost/AppHost.cs`:

- Creates a SQL Server resource named `sql`
- Creates a database named `aonikdb`
- References that SQL resource from the API and Worker
- Marks the container lifetime as persistent (data is retained between runs unless you remove the container)

Run it:

```bash
dotnet run --project src/Aonik.AppHost
```

Then:

- API endpoints run under the AppHost orchestration.
- The Aspire dashboard/telemetry endpoints are configured via `src/Aonik.AppHost/Properties/launchSettings.json`.

## Running The API Without Aspire (LocalDB)

The API has a Development connection string in `src/Aonik.Api/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Run the API directly:

```bash
dotnet run --project src/Aonik.Api
```

Swagger is available at `/swagger` when running in Development.

## Using InMemory In Development (Optional)

If you want a no-SQL local experience for quick prototyping, set:

- `UseInMemoryDatabase=true`
- `InMemoryDatabaseName=SomeName` (optional)

Examples:

PowerShell:

```powershell
$env:UseInMemoryDatabase = "true"
$env:InMemoryDatabaseName = "AonikDevInMemory"
dotnet run --project src/Aonik.Api
```

## Bootstrap The First Tenant (Development Only)

When running locally with no tenants in the database, you can use the dev-only bootstrap endpoint
to create the initial tenant and assign the current user the **TenantAdmin** role.

1. Run the API (`dotnet run --project src/Aonik.Api`).
2. Send a POST request to `/bootstrap` with a valid access token.


Example (replace `$TOKEN` with a bearer token from your IdP):

```bash
curl -X POST https://localhost:5001/bootstrap \
  -H "Authorization: Bearer $TOKEN"
```

Notes:
- The endpoint is available when no tenants exist.
- In non-Development environments, the caller must have the `PlatformAdmin` claim.
- The created tenant uses the default values in the `Bootstrap` configuration section.


## Migrations

AONIK uses EF Core migrations from the Infrastructure project.

```bash
# Add a migration
dotnet ef migrations add <MigrationName> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Apply migrations
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

Note: InMemory does not use migrations.

## Troubleshooting

- If the API fails fast in non-Development environments, set `ConnectionStrings:DefaultConnection` (or `ConnectionStrings__DefaultConnection` as an env var).
- If Aspire starts containers but the API still uses LocalDB, ensure `ConnectionStrings:DefaultConnection` is coming from the environment you expect (environment variables override JSON).
