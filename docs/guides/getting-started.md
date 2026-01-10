# Getting Started

This guide gets you running locally with either Aspire (recommended) or a standalone API.

## Prerequisites

- .NET SDK (`net10.0`)
- Git

Choose a database option:

- **Aspire + Docker (recommended)**: Docker Desktop or compatible Docker engine
- **LocalDB (no Docker)**: SQL Server LocalDB (commonly installed with Visual Studio)

## Option A: Run With Aspire (Recommended)

This runs API + Worker + SQL Server (Docker) via the AppHost:

```bash
dotnet run --project src/Aonik.AppHost
```

Aspire provisions SQL Server based on `src/Aonik.AppHost/AppHost.cs`.

## Option B: Run The API Directly (Development)

```bash
dotnet run --project src/Aonik.Api
```

Development uses `src/Aonik.Api/appsettings.Development.json` for `ConnectionStrings:DefaultConnection` and falls back to LocalDB if missing.

## Database Provider Selection

The provider selection rules are documented in `docs/deployment/local-development.md`.

## Next Steps

- Explore Swagger at `/swagger` (Development)
- Run tests: `dotnet test Aonik.sln`
