# DbContext Improvements & Tenant Scoping Strategy

## Overview
This document outlines the improvements made to `AonikDbContext` and provides a strategy for implementing tenant-based query filters to ensure data isolation.

## Completed Improvements

### 1. Fixed Naming Consistency
- **Issue**: `PersonalFinanceProfiles` inconsistent with standard naming convention
- **Solution**: Renamed to `PersonalProfiles` to match entity name and conventions
- **Impact**: Table will be named `PersonalProfiles` by EF Core convention

### 2. Simplified Type References
- **Issue**: Fully qualified type names cluttered the DbContext (e.g., `Aonik.Domain.Party.Entities.PartyAddress`)
- **Solution**: Added using aliases for all ambiguous types:
  ```csharp
  using LedgerEntity = Aonik.Domain.Ledger.Entities.Ledger;
  using PartyEntity = Aonik.Domain.Party.Entities.Party;
  using PartyAddress = Aonik.Domain.Party.Entities.PartyAddress;
  // ... etc
  ```
- **Impact**: Cleaner, more readable DbContext while maintaining type safety

### 3. Verified Insight and Signal Entities
- **Status**: Confirmed as legitimate domain entities (not projections)
- **Purpose**: Store AI-generated insights and signals/alerts
- **Configuration**: Already have proper `IEntityTypeConfiguration<>` implementations

### 4. Comprehensive Entity Coverage
All 80+ domain entities registered across 14 business modules:
- Identity (6 entities)
- Party (8 entities)
- Ledger (5 entities)
- Payments (5 entities)
- Billing (5 entities)
- Partners (6 entities)
- Pricing (3 entities)
- Compliance (3 entities)
- Operations (2 entities)
- Notifications (2 entities)
- AI (13 entities)
- Agents (4 entities)
- Orders (6 entities)
- Personal Finance (10 entities)

## Tenant Scoping Strategy

### Entities by Tenant Scope

#### A. Tenant-Scoped Entities (Required TenantId)
**55 entities with `public Guid TenantId`**

These entities **MUST** be scoped to a specific tenant:
- All Identity entities (except Tenant itself)
- All Party entities
- All Ledger entities
- All Payments entities
- All Billing entities
- All Partners entities
- All Pricing entities
- All Compliance entities
- All Operations entities
- All Notifications entities
- All Orders entities
- All Personal Finance entities
- AI: AiRun
- Agents: AgentRun, Proposal

#### B. Global/Optional Entities (Nullable TenantId)
**3 entities with `public Guid? TenantId`**

These entities can be global (shared) or tenant-specific:
- `Agent` - Can define global or tenant-specific agents
- `OrchestratorPolicy` - Can define global or tenant-specific orchestration policies
- `AiRoutePolicy` - Can define global or tenant-specific AI routing rules

#### C. Global-Only Entities (No TenantId)
**Remaining entities without TenantId property**

These are truly global entities shared across all tenants:
- `Tenant` itself
- AI infrastructure: `AiProvider`, `AiModel`, `PromptSpec`, `ToolSpec`, `AiPolicy`, `AiTrace`, `AiFeedback`, `EvalSuite`, `EvalRun`
- Signals and Insights (if truly global)
- Party sub-entities: `PartyAddress`, `PartyContact`, `PartyConsent`, `PersonProfile`, `BusinessProfile`
- Ledger sub-entities: `JournalEntryLine`, `BalanceSnapshot`
- Others: `HouseholdMember`, etc.

### Recommended Implementation Plan

#### Phase 1: Define ITenantScoped Interface (SharedKernel)
```csharp
namespace Aonik.SharedKernel.Primitives;

public interface ITenantScoped
{
    Guid TenantId { get; }
}
```

#### Phase 2: Create Tenant Context Provider (Infrastructure)
```csharp
namespace Aonik.Infrastructure.Persistence;

public interface ITenantProvider
{
    Guid GetCurrentTenantId();
}

public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentTenantId()
    {
        // Extract from JWT claims, header, or route
        var tenantIdClaim = _httpContextAccessor.HttpContext?
            .User?.Claims.FirstOrDefault(c => c.Type == "tenant_id");

        if (tenantIdClaim == null || !Guid.TryParse(tenantIdClaim.Value, out var tenantId))
        {
            throw new UnauthorizedAccessException("Tenant context not found");
        }

        return tenantId;
    }
}
```

#### Phase 3: Apply Global Query Filters (AonikDbContext)
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Apply all configurations from this assembly
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

    // Apply tenant filters to all tenant-scoped entities
    ApplyTenantQueryFilters(modelBuilder);
}

private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
{
    // Only apply filters if tenant provider is available
    if (_tenantProvider == null) return;

    var tenantId = _tenantProvider.GetCurrentTenantId();

    // Apply to all entities with required TenantId
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        var clrType = entityType.ClrType;

        // Check if entity has non-nullable TenantId property
        var tenantIdProperty = clrType.GetProperty("TenantId");
        if (tenantIdProperty != null &&
            tenantIdProperty.PropertyType == typeof(Guid))
        {
            var parameter = Expression.Parameter(clrType, "e");
            var property = Expression.Property(parameter, "TenantId");
            var tenantIdValue = Expression.Constant(tenantId);
            var equality = Expression.Equal(property, tenantIdValue);
            var lambda = Expression.Lambda(equality, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }
}
```

#### Phase 4: Handle Optional Tenant Scoping
For entities with nullable TenantId, apply more nuanced filters:
```csharp
// In entity configuration (e.g., AgentConfiguration.cs)
public void Configure(EntityTypeBuilder<Agent> builder)
{
    // ... other configuration

    // Filter: global agents (TenantId == null) OR tenant-specific agents
    // This will be combined with the tenant filter if applied
    builder.HasIndex(x => x.TenantId);
}
```

### Security Considerations

1. **Service Layer Enforcement**: Query filters are a safety net, not primary security
2. **Write Operations**: Query filters only apply to reads - validate TenantId on writes
3. **Admin Operations**: Provide a way to bypass filters for system admin operations
4. **Testing**: Use separate DbContext instances with/without tenant filters for testing

### Performance Considerations

1. **Indexes**: Ensure all TenantId columns have indexes (should be in entity configurations)
2. **Composite Keys**: Consider composite indexes on (TenantId, OtherKey) for common queries
3. **Query Filter Overhead**: Minimal - EF Core applies filters efficiently in SQL WHERE clauses

### Migration Path

1. ✅ **Completed**: Register all entities in DbContext
2. ✅ **Completed**: Clean up type references and naming
3. **Next**: Implement ITenantProvider
4. **Next**: Apply query filters in OnModelCreating
5. **Next**: Add comprehensive integration tests
6. **Next**: Update entity configurations with proper indexes

## Configuration Files Status

### Existing Configurations
The following entity configurations already exist:
- ✅ InvoiceLineConfiguration (renamed from InvoiceLineItemConfiguration)
- ✅ SignalConfiguration
- ✅ InsightConfiguration
- ✅ PaymentIntentConfiguration
- ✅ InvoiceConfiguration
- ✅ JournalEntryConfiguration
- ✅ LedgerAccountConfiguration

### Missing Configurations
The remaining 73+ entities need `IEntityTypeConfiguration<>` implementations to define:
- Primary keys (if not using Id convention)
- Foreign keys and relationships
- Required/optional properties
- String max lengths
- Decimal precision
- Indexes (especially on TenantId and foreign keys)
- Table names (if not using convention)
- Unique constraints

## Next Actions

1. **High Priority**:
   - [ ] Create ITenantProvider interface and implementation
   - [ ] Apply tenant query filters in OnModelCreating
   - [ ] Create entity configurations for critical entities (User, Party, Order, Payment, Invoice)
   - [ ] Add comprehensive integration tests for tenant isolation

2. **Medium Priority**:
   - [ ] Create entity configurations for remaining entities
   - [ ] Document tenant scoping behavior in domain entity comments
   - [ ] Add admin bypass mechanism for query filters
   - [ ] Performance test with large multi-tenant datasets

3. **Low Priority**:
   - [ ] Consider using `DbSet<T>.IgnoreQueryFilters()` for admin queries
   - [ ] Add logging/telemetry for tenant context resolution
   - [ ] Document entity relationship diagrams by module

## References

- [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [Multi-tenancy Patterns](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models)
- [EF Core Entity Configuration](https://learn.microsoft.com/en-us/ef/core/modeling/entity-types)
