:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Tenant Management

## Overview

AONIK is a **multi-tenant SaaS platform** where each tenant's data is completely isolated. We use a **shared database with tenant ID filtering** approach, enforced at the database layer using EF Core global query filters.

## Architecture

### Tenant Isolation Strategy

- **Shared Database**: All tenants share the same database
- **Row-Level Security**: Each row has a `TenantId` column
- **Global Query Filters**: EF Core automatically filters by tenant
- **Compile-Time Safety**: Entities implement `ITenantScoped` interface

### Benefits

✅ **Simplicity**: Single database to manage  
✅ **Cost-Effective**: Shared infrastructure  
✅ **Easy Backups**: Single backup strategy  
✅ **Query Efficiency**: Can optimize across tenants  

### Trade-offs

⚠️ **Scaling Limits**: Single database has limits  
⚠️ **Noisy Neighbor**: One tenant can impact others  
⚠️ **Security Risk**: Data leak if filter fails  

## Implementation

### 1. Tenant-Scoped Interface

All tenant-specific entities implement `ITenantScoped`:

```csharp
// In SharedKernel
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
```

### 2. Entity Implementation

```csharp
public class Invoice : AuditableEntity, ITenantScoped
{
    public Guid InvoiceId { get; set; }
    public Guid TenantId { get; set; }  // Required for tenant isolation
    public Guid CustomerAccountId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<InvoiceLine> Lines { get; set; } = new();
}
```

### 3. Global Query Filters

EF Core automatically filters all queries by the current tenant:

```csharp
// In AonikDbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Apply global query filter to all ITenantScoped entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
            var tenantId = Expression.Constant(_tenantProvider.GetTenantId());
            var filter = Expression.Lambda(Expression.Equal(property, tenantId), parameter);

            entityType.SetQueryFilter(filter);
        }
    }
}
```

### 3.1 Global visibility for nullable tenant entities

Some AI/agent configuration entities allow a nullable `TenantId` so they can be shared globally. These entities apply a filter that keeps **tenant-owned rows** plus **global rows**:

```csharp
// Agent, OrchestratorPolicy, AiRoutePolicy
entity => entity.TenantId == currentTenantId || entity.TenantId == null
```

### 4. Write-time tenant safeguards

`AonikDbContext.SaveChangesAsync` enforces tenant safety for writes:

- **Added** tenant-scoped entities with `TenantId == Guid.Empty` are assigned the current tenant.
- **Modified/Deleted** tenant-scoped entities must match the current tenant, otherwise an exception is thrown.
- Any tenant-scoped write without an available tenant context throws immediately.

### 5. Tenant Provider

The `ITenantProvider` interface provides the current tenant context:

```csharp
public interface ITenantProvider
{
    Guid GetTenantId();
}

// HTTP Context implementation
public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetTenantId()
    {
        // Extract from JWT claims, header, or subdomain
        var tenantIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst("tenant_id")?.Value;

        return Guid.TryParse(tenantIdClaim, out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("Tenant ID not found in request context");
    }
}
```

## Usage in Services

Services automatically get tenant-filtered data:

```csharp
public class BillingService : IBillingService
{
    private readonly IAonikDbContext _dbContext;

    public BillingService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<InvoiceResponse>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
        // This query is AUTOMATICALLY filtered by TenantId!
        var invoices = await _dbContext.Invoices
            .Include(i => i.Lines)
            .ToListAsync(cancellationToken);

        return invoices.Select(MapToResponse).ToList();
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Must set TenantId when creating new entities
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty, // TODO: Get from ITenantProvider
            CustomerAccountId = request.CustomerId,
            // ... other properties
        };

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(invoice);
    }
}
```

## Bypassing Tenant Filters

In rare cases (e.g., system admin operations), you can bypass filters:

```csharp
// WARNING: Only for system-level operations!
var allInvoices = await _dbContext.Invoices
    .IgnoreQueryFilters()  // Bypasses tenant filter
    .ToListAsync(cancellationToken);
```

⚠️ **Use with extreme caution** - only in admin/system operations.

## Multi-Tenant Entities

### Tenant-Scoped (Most Entities)

These belong to a specific tenant:

- ✅ `Invoice` - Tenant-specific invoices
- ✅ `Payment` - Tenant-specific payments
- ✅ `Party` - Tenant-specific customers/vendors
- ✅ `LedgerAccount` - Tenant-specific accounts

### Global/Shared Entities

These are shared across all tenants:

- ❌ `Tenant` - The tenant itself
- ❌ `User` - Users can belong to multiple tenants
- ❌ `AiModel` - Shared AI model configurations
- ❌ `AiProvider` - Shared AI provider configs

## Tenant Identification

### Option 1: Subdomain-based

```
https://acme-corp.aonik.io     → Tenant: acme-corp
https://globex.aonik.io        → Tenant: globex
```

### Option 2: JWT Claim

```json
{
  "sub": "user-123",
  "tenant_id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@acme-corp.com"
}
```

### Option 3: Header-based

```http
GET /api/invoices
X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer <token>
```

## Database Schema

### Example: Invoices Table

```sql
CREATE TABLE Invoices (
    Id              uniqueidentifier NOT NULL PRIMARY KEY,
    InvoiceId       uniqueidentifier NOT NULL,
    TenantId        uniqueidentifier NOT NULL,  -- Tenant isolation
    CustomerAccountId uniqueidentifier NOT NULL,
    Currency        nvarchar(3) NOT NULL,
    Total           decimal(18,2) NOT NULL,
    Status          nvarchar(50) NOT NULL,
    CreatedAt       datetime2 NOT NULL,

    INDEX IX_Invoices_TenantId (TenantId),  -- Critical for performance!
    INDEX IX_Invoices_TenantId_Status (TenantId, Status)
);
```

⚠️ **Critical**: Always index `TenantId` for query performance!

## Testing with Tenants

### Test with Multiple Tenants

```csharp
[Fact]
public async Task GetInvoicesAsync_ShouldOnlyReturnTenantInvoices()
{
    // Arrange
    var tenant1 = Guid.NewGuid();
    var tenant2 = Guid.NewGuid();

    var options = new DbContextOptionsBuilder<AonikDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    using var context = new AonikDbContext(options, new FakeTenantProvider(tenant1));

    // Seed data for both tenants
    context.Invoices.AddRange(
        new Invoice { Id = Guid.NewGuid(), TenantId = tenant1, Total = 100 },
        new Invoice { Id = Guid.NewGuid(), TenantId = tenant1, Total = 200 },
        new Invoice { Id = Guid.NewGuid(), TenantId = tenant2, Total = 300 }
    );
    await context.SaveChangesAsync();

    var service = new BillingService(context);

    // Act
    var invoices = await service.GetInvoicesAsync();

    // Assert
    invoices.Should().HaveCount(2);  // Only tenant1 invoices
    invoices.Should().AllSatisfy(i => i.TenantId.Should().Be(tenant1));
}
```

## Security Considerations

### ✅ DO:

1. **Always use global query filters** - Don't rely on manual filtering
2. **Index TenantId columns** - Performance is critical
3. **Validate tenant access** - Ensure user belongs to tenant
4. **Test cross-tenant scenarios** - Verify data isolation
5. **Audit cross-tenant access** - Log when filters are bypassed

### ❌ DON'T:

1. **Never expose TenantId in URLs** - Use JWT or headers
2. **Don't bypass filters carelessly** - `IgnoreQueryFilters()` is dangerous
3. **Don't forget TenantId on new entities** - Will fail or leak data
4. **Don't trust client-provided TenantId** - Always validate server-side
5. **Don't skip tenant validation in tests** - Test isolation thoroughly

## Migration to Multi-Tenancy

If adding multi-tenancy to existing tables:

```csharp
public partial class AddTenantSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add TenantId column (nullable initially)
        migrationBuilder.AddColumn<Guid>(
            name: "TenantId",
            table: "Invoices",
            nullable: true);

        // 2. Set default tenant for existing data
        migrationBuilder.Sql(
            "UPDATE Invoices SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL");

        // 3. Make TenantId required
        migrationBuilder.AlterColumn<Guid>(
            name: "TenantId",
            table: "Invoices",
            nullable: false);

        // 4. Add index
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId",
            table: "Invoices",
            column: "TenantId");
    }
}
```

## Monitoring & Alerts

### Key Metrics to Track

1. **Cross-tenant queries** - Should be zero in production
2. **Queries without TenantId filter** - Indicates missing `ITenantScoped`
3. **Failed tenant identification** - User without valid tenant claim
4. **Tenant data size** - Monitor for runaway growth

## Future Enhancements

### Potential Improvements

1. **Tenant-specific database sharding** - For scaling beyond single DB
2. **Tenant-level feature flags** - Enable features per tenant
3. **Tenant-specific rate limiting** - Fair resource sharing
4. **Tenant analytics** - Usage metrics per tenant
5. **Tenant backup/restore** - Individual tenant operations

## References

- [DbContext Configuration](../database/schema-overview.md)
- [Entity Guidelines](../guides/domain-entities.md)
- Tenant isolation ADR (planned)
