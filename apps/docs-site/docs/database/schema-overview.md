# Schema Overview

AONIK uses EF Core to map domain entities to a SQL Server schema.

## Key points

- DbContext: `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs`
- Migrations: `src/Aonik.Infrastructure/Persistence/Migrations/`

## Modules

The schema is organized by modules such as Billing, Payments, Ledger, and Identity.

For migration commands, see [Database Migrations](../guides/database-migrations.md).
