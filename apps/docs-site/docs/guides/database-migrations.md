# Database Migrations

Use the migrator for first install and repeatable environment setup.

## First Install (Recommended)

```bash
dotnet run --project src/Aonik.Migrator
```

This runs:
- All registered database migrations (in order)
- Global base seed data (permissions, catalog/reference data, global settings)

## Migrator Modes

```bash
# Run migrations only
dotnet run --project src/Aonik.Migrator -- --migrate-only

# Run seed routines only
dotnet run --project src/Aonik.Migrator -- --seed-only
```

## Creating New Migrations

```bash
dotnet ef migrations add <MigrationName> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

## Manual EF Fallback (Only If Needed)

If you must run EF commands directly, apply in this order:

```bash
# 1) Monolith/aggregate migration history
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# 2) Platform schema-move migration(s)
dotnet ef database update --project src/Aonik.Platform --startup-project src/Aonik.Api --context PlatformDbContext
```

## Notes

- InMemory does not use migrations.
- When running with Aspire, the API still reads `ConnectionStrings:DefaultConnection`; Aspire supplies it at runtime.
