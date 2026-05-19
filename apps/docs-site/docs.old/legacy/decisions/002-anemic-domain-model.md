:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# ADR 002: Adopt Anemic Domain Model

**Status**: Accepted  
**Date**: 2026-01-08  
**Decision Makers**: Development Team  
**Related**: [001-custom-ai-implementation-vs-maf.md](001-custom-ai-implementation-vs-maf.md)

## Context

AONIK initially used a Domain-Driven Design (DDD) approach with rich domain entities containing business logic, state transitions, and collection management methods. This pattern, while theoretically sound, introduced complexity and friction in a codebase that:

1. Primarily serves as a data management and transformation layer
2. Uses EF Core as the persistence mechanism
3. Has business logic that often spans multiple aggregates
4. Requires flexible querying capabilities
5. Benefits from straightforward CRUD operations

### Previous Pattern (Rich Domain Model)

```csharp
public class Invoice : AuditableEntity
{
    private readonly List<InvoiceLine> _lines = new();
    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();
    
    private Invoice() { }  // EF Core constructor
    
    public Invoice(Guid customerId, string currency)
    {
        CustomerId = customerId;
        Currency = currency;
        Status = InvoiceStatus.Draft;
    }
    
    public void AddLine(InvoiceLine line)
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Cannot modify issued invoice");
        _lines.Add(line);
        RecalculateTotals();
    }
    
    public void Issue()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be issued");
        Status = InvoiceStatus.Issued;
        IssuedAt = DateTime.UtcNow;
    }
}
```

### Problems Encountered

1. **ORM Friction**: EF Core requires parameterless constructors and writable properties, leading to awkward patterns (`private set`, EF-specific constructors)
2. **Testing Complexity**: Tests need to set up full entity state through constructors and method calls
3. **Cross-Aggregate Logic**: Business rules often need to coordinate multiple entities, pushing logic to services anyway
4. **Collection Management**: Readonly collection patterns add boilerplate without significant protection
5. **State Reconstruction**: Loading entities from database requires bypassing domain logic
6. **Query Limitations**: Rich entities don't help with complex queries that need to work at the data layer

## Decision

We will adopt an **anemic domain model** where:

1. **Entities are pure data containers** with no business logic
2. **All business logic resides in application services**
3. **Properties use public getters and setters** (`{ get; set; }`)
4. **Collections are simple `List<T>`** properties
5. **No constructors** - entities created via object initializers
6. **No methods** - all behavior in services

### New Pattern (Anemic Domain Model)

```csharp
public class Invoice : AuditableEntity, ITenantScoped
{
    public Guid InvoiceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerAccountId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? IssuedAt { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();
}

// Business logic in service
public class BillingService : IBillingService
{
    public async Task IssueInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            
        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");
            
        if (invoice.Status != "Draft")
            throw new InvalidOperationException("Only draft invoices can be issued");
            
        invoice.Status = "Issued";
        invoice.IssuedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }
}
```

## Rationale

### Benefits

1. **Simplicity**: Straightforward, easy-to-understand code
2. **ORM Harmony**: Works naturally with EF Core's expectations
3. **Testing Ease**: Simple object initialization in tests
4. **Flexibility**: Easy to query, filter, and project data
5. **Pragmatism**: Acknowledges that most business logic needs database context and crosses aggregate boundaries
6. **Reduced Boilerplate**: No readonly collections, no private fields, no complex constructors
7. **YAGNI Principle**: Don't add complexity unless it provides clear value

### Trade-offs

1. **No Encapsulation**: Entities can be modified from anywhere (mitigated by service layer discipline)
2. **No Invariant Protection**: Must rely on service validation (acceptable given our architecture)
3. **Not "Pure" DDD**: Breaks DDD principles (acceptable - pragmatism over purity)

### Why This Makes Sense for AONIK

1. **Transaction Script Pattern**: Most operations are straightforward CRUD with validation
2. **Service-Oriented**: Business logic naturally lives in services that coordinate multiple entities
3. **Multi-Tenancy**: Tenant isolation happens at DbContext level, not domain level
4. **AI Integration**: AI workflows need to read/write data flexibly across many entities
5. **Team Velocity**: Simpler patterns = faster development = more features delivered

## Consequences

### Positive

- **Faster development**: Less time fighting with entity design
- **Easier onboarding**: New developers understand the pattern immediately
- **Better testability**: Simple object creation in tests
- **EF Core optimization**: Can leverage all EF Core features without workarounds

### Negative

- **Discipline required**: Developers must use services consistently
- **Validation centralization**: All validation logic must be in services or request validators
- **No compile-time protection**: Can't rely on entity methods to enforce rules

### Migration Impact

- **79+ entities refactored** to remove methods and expose properties
- **Services updated** to contain business logic previously in entities
- **Tests need rewriting** to work with new pattern (currently failing)
- **Documentation updated** to reflect new patterns

## Implementation Guidelines

### Entity Pattern

```csharp
public class PaymentIntent : AuditableEntity, ITenantScoped
{
    public Guid PaymentIntentId { get; set; }
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<PaymentItem> Items { get; set; } = new();
}
```

### Service Pattern

```csharp
public class PaymentService : IPaymentService
{
    private readonly IAonikDbContext _dbContext;
    
    public async Task CapturePaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
            
        if (payment == null)
            throw new InvalidOperationException($"Payment {paymentId} not found");
            
        if (payment.Status != "Authorized")
            throw new InvalidOperationException("Only authorized payments can be captured");
            
        payment.Status = "Captured";
        await _dbContext.SaveChangesAsync(ct);
    }
}
```

## References

- [AGENTS.md - Domain & Entity Patterns](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md#domain--entity-patterns)
- [Application Services Guide](../guides/application-services.md)
- [Martin Fowler - Anemic Domain Model](https://martinfowler.com/bliki/AnemicDomainModel.html) (Anti-pattern we're consciously accepting)
- Commit: `7069f9a` - "Refactor to anemic domain model: move all business logic to services"
- Commit: `7c7f3ee` - "Update AuditableEntity to use public setters for anemic model"

## Review

This decision should be revisited if:
- Complex business rules emerge that truly benefit from encapsulation
- The team finds that lack of entity validation causes significant bugs
- We adopt Event Sourcing or similar patterns that require rich domain models
- Performance profiling shows that services are a bottleneck

**Next Review Date**: 2026-07-08 (6 months)
