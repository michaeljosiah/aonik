# Tenant Isolation

AONIK supports multi-tenancy using `ITenantScoped` and EF Core query filters.

## How it works

- Entities that are tenant-scoped implement `ITenantScoped`.
- `AonikDbContext` applies a query filter for tenant-scoped entities when a tenant is available.

See `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs` for the filter logic.
