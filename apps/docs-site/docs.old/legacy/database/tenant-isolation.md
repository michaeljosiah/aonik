:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Tenant Isolation

AONIK supports multi-tenancy using `ITenantScoped` and EF Core query filters.

## How it works

- Entities that are tenant-scoped implement `ITenantScoped`.
- `AonikDbContext` applies a query filter for tenant-scoped entities when a tenant is available.

See `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs` for the filter logic.
