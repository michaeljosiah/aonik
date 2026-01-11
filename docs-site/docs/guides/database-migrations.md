# Database Migrations

AONIK uses EF Core migrations from the Infrastructure project.

## Prerequisites

```bash
dotnet tool install --global dotnet-ef
```

## Create a migration

```bash
dotnet ef migrations add <MigrationName> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

## Apply migrations

```bash
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

## Notes

- InMemory does not use migrations.
- When running with Aspire, the API still reads `ConnectionStrings:DefaultConnection`; Aspire supplies it at runtime.
