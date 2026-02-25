# Tenant Isolation

AONIK supports multi-tenancy using `ITenantScoped` and EF Core query filters.

## How it works

- Entities that are tenant-scoped implement `ITenantScoped` (defined in SharedKernel).
- `AonikDbContextBase` (the shared abstract base for all DbContexts) applies global query filters for tenant-scoped entities when a tenant is available.
- Write-time safeguards in `SaveChangesAsync` enforce that new entities get the current tenant ID and that modifications/deletions cannot cross tenant boundaries.
- Some AI/agent configuration entities allow nullable `TenantId` for global visibility (e.g., `Agent`, `OrchestratorPolicy`, `AiRoutePolicy`).

## DbContext hierarchy

All module-scoped DbContexts inherit tenant isolation from the base:

```
AonikDbContextBase (SharedKernel — abstract)
  ├── AonikDbContext       (Infrastructure — monolithic, for migrations)
  ├── PlatformDbContext    (Platform module)
  ├── FinanceDbContext     (Finance module)
  ├── AiDbContext          (AI module)
  └── AgentsDbContext      (Agents module)
```

See [Tenant Management](../features/tenant-management.md) for full implementation details including tenant identification, testing patterns, and security considerations.
