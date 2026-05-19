:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Schema Overview

AONIK uses EF Core to map domain entities to a SQL Server schema.

## Key points

- DbContext: `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs`
- Migrations: `src/Aonik.Infrastructure/Persistence/Migrations/`

## Modules

The schema is organized by modules such as Billing, Payments, Ledger, and Identity.

For migration commands, see [Database Migrations](../guides/database-migrations.md).
