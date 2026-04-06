# Backend Performance Review (AONIK)

Scope: Backend code only (Application/Domain/Infrastructure/Api/Worker). Admin UI intentionally excluded.

## Critical Severity — High Impact

### 1) N+1 query pattern in tenant listing
**Impact:** Listing tenants triggers additional queries per tenant to load supported countries and currencies, which can multiply database round-trips and latency as tenant count grows.

**Evidence:** `ListTenantsAsync` loads tenants first, then calls `MapToResponseAsync` for each tenant, which issues separate queries for supported country/currency codes per tenant. This results in at least two extra queries per tenant.

**Recommendation:** Batch-load tenant countries/currencies and map in-memory, or project the needed data using joins/aggregates in a single query.

**References:**
- `src/Aonik.Application/Services/Identity/TenantService.cs` (`ListTenantsAsync` and `MapToResponseAsync`)

## High Severity — Medium/High Impact

### 2) Correlated subqueries for role counts in user listing
**Impact:** `ListUsersAsync` performs a `Count` subquery for each user row, which can become expensive at scale and may result in correlated subqueries per row depending on the provider.

**Evidence:** `ListUsersAsync` uses `_dbContext.UserRoles.Count(ur => ur.UserId == user.Id)` inside the projection.

**Recommendation:** Pre-aggregate role counts (group-by) and join, or project to a DTO with a separate batched query.

**References:**
- `src/Aonik.Application/Services/Identity/AccessManagementService.cs` (`ListUsersAsync`)

### 3) Correlated subqueries for permission/user counts in role listing
**Impact:** `ListRolesAsync` performs two counts per role in the projection. On large datasets this can add significant query cost.

**Evidence:** `ListRolesAsync` uses `_dbContext.RolePermissions.Count(...)` and `_dbContext.UserRoles.Count(...)` inside the projection.

**Recommendation:** Replace per-row counts with pre-aggregated group-by queries and join or materialize counts once.

**References:**
- `src/Aonik.Application/Services/Identity/AccessManagementService.cs` (`ListRolesAsync`)

## Medium Severity — Medium Impact

### 4) Unbounded relationship fetch without pagination
**Impact:** `GetRelationshipsAsync` loads all relationships for a party and then loads all involved parties into memory. Large relationship graphs can cause high memory usage and slow response times.

**Evidence:** `GetRelationshipsAsync` uses `ToListAsync` and then materializes distinct IDs into memory without paging.

**Recommendation:** Add pagination parameters and limit the result set. Consider projecting directly into the response with a single query.

**References:**
- `src/Aonik.Application/Services/Parties/PartyService.cs` (`GetRelationshipsAsync`)

### 5) Tenant list query tracks entities unnecessarily
**Impact:** `ListTenantsAsync` returns a read-only list but does not use `AsNoTracking`, causing unnecessary change tracking overhead.

**Evidence:** `_dbContext.Tenants.AsQueryable()` is used without `AsNoTracking` in `ListTenantsAsync`.

**Recommendation:** Apply `AsNoTracking()` for read-only queries.

**References:**
- `src/Aonik.Application/Services/Identity/TenantService.cs` (`ListTenantsAsync`)

## Low Severity — Low/Medium Impact

### 6) Repeated in-memory filtering when building relationship names
**Impact:** `LoadPartyNamesAsync` loads both parties into memory and then uses `FirstOrDefault` twice. This is minor but avoidable with a dictionary or direct projection.

**Evidence:** `LoadPartyNamesAsync` calls `.ToListAsync(...)` and then `FirstOrDefault(...)` for each id.

**Recommendation:** Convert the list to a dictionary or fetch names in a single projection keyed by ID.

**References:**
- `src/Aonik.Application/Services/Parties/PartyService.cs` (`LoadPartyNamesAsync`)

---

## Resolution Checklist

- [ ] Address N+1 queries in tenant listing (batch-load or join tenant countries/currencies).
- [ ] Replace per-user role count correlated subquery in user listing with batched aggregation.
- [ ] Replace per-role counts in role listing with batched aggregation.
- [ ] Add pagination to party relationship listing or a bounded result strategy.
- [ ] Add `AsNoTracking()` to tenant listing query.
- [ ] Optimize party name lookups to avoid repeated in-memory filtering.
