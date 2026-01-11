# ADR 003: No Generic Repository Pattern Over EF Core

**Status**: Accepted  
**Date**: 2026-01-08  
**Decision Makers**: Development Team  
**Related**: [002-anemic-domain-model.md](002-anemic-domain-model.md)

## Context

When using Entity Framework Core as an ORM, a common pattern is to introduce a generic repository abstraction layer:

```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    IQueryable<T> Query();
}
```

This pattern was popularized in earlier versions of Entity Framework and is still taught in many tutorials. The question arose: **Should AONIK implement a generic repository pattern over EF Core's DbContext?**

## Decision

**We will NOT implement a generic repository pattern.** Instead, services will interact directly with `IAonikDbContext` and its `DbSet<T>` properties.

### Current Pattern

```csharp
public class BillingService : IBillingService
{
    private readonly IAonikDbContext _dbContext;
    
    public BillingService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            
        return invoice == null ? null : MapToResponse(invoice);
    }
}
```

### What We're NOT Doing

```csharp
// NOT implementing this
public class BillingService : IBillingService
{
    private readonly IRepository<Invoice> _invoiceRepository;
    
    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _invoiceRepository.Query()
            .Include(i => i.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
            
        return invoice == null ? null : MapToResponse(invoice);
    }
}
```

## Rationale

### Why Generic Repositories Were Popular

1. **Legacy EF Concerns**: Early Entity Framework had poor testability
2. **Database Abstraction**: Theoretically easier to swap ORMs
3. **Hide Implementation**: Prevent services from knowing about data access details

### Why We Don't Need Them with Modern EF Core

1. **DbContext IS a Repository**: EF Core's `DbContext` already implements both Unit of Work and Repository patterns
   - `DbSet<T>` is effectively a repository per entity
   - `SaveChangesAsync()` is the Unit of Work transaction boundary
   - Change tracking provides the identity map pattern

2. **Abstraction Over Abstraction**: Generic repositories add a layer that provides no real value
   - `DbSet<T>` already provides `Add()`, `Remove()`, `Find()`, `Where()`, etc.
   - `IQueryable<T>` already provides powerful querying
   - We'd just be wrapping EF Core methods with identical signatures

3. **Lost Functionality**: Generic repositories limit EF Core's power
   - Can't use advanced features like `Include()`, `AsSplitQuery()`, `AsNoTracking()`
   - Can't leverage EF Core's query optimization
   - Difficult to support projections and complex joins

4. **Testing is Easy**: EF Core has excellent testability
   - In-memory database for integration tests
   - DbContext can be easily mocked if needed
   - `IAonikDbContext` interface provides test seam

5. **YAGNI Principle**: We aren't going to need to swap ORMs
   - Switching from EF Core to another ORM is extremely unlikely
   - Even if we did, query syntax differences would require service rewrites anyway
   - Generic repositories wouldn't actually hide EF Core-specific code

6. **Specification Pattern Available**: If query reuse is needed, we can use Specification pattern
   - Can create reusable query expressions
   - Can compose specifications
   - More flexible than repository methods

## Consequences

### Positive

- **Full EF Core Power**: Services can use all EF Core features
- **Less Boilerplate**: No repository interfaces and implementations to maintain
- **Better Performance**: Direct access allows for query optimization
- **Clearer Code**: Obvious what's happening in data access
- **Easier Debugging**: Can see exact EF Core queries being generated

### Negative

- **EF Core Coupling**: Services are coupled to EF Core
  - **Mitigation**: We accept this trade-off; EF Core is a stable, mature library
- **More Complex Service Tests**: Tests need to set up DbContext
  - **Mitigation**: Use in-memory database; it's straightforward
- **No Centralized Query Logic**: Each service writes its own queries
  - **Mitigation**: Use extension methods or specifications for shared query logic

### Testing Strategy

**For Unit Tests**: Mock `IAonikDbContext` if testing pure business logic

**For Integration Tests**: Use EF Core's in-memory database

```csharp
var options = new DbContextOptionsBuilder<AonikDbContext>()
    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
    .Options;
    
using var context = new AonikDbContext(options);
var service = new BillingService(context);
```

**For Complex Queries**: Create reusable specifications or extension methods

```csharp
public static class InvoiceQueryExtensions
{
    public static IQueryable<Invoice> WithLines(this IQueryable<Invoice> query)
    {
        return query.Include(i => i.Lines);
    }
    
    public static IQueryable<Invoice> ForTenant(this IQueryable<Invoice> query, Guid tenantId)
    {
        return query.Where(i => i.TenantId == tenantId);
    }
}

// Usage
var invoice = await _dbContext.Invoices
    .WithLines()
    .ForTenant(tenantId)
    .FirstOrDefaultAsync(i => i.Id == invoiceId);
```

## Industry Perspective

### Modern .NET Best Practices

- **Microsoft's Official Guidance**: EF Core docs recommend using DbContext directly
- **Clean Architecture**: Robert C. Martin's examples show repositories for true persistence abstraction, not over ORMs
- **Pragmatic Programmer**: Choose abstractions that provide value, not ceremony

### When Repositories Make Sense

- **Multiple Data Sources**: If the same entity comes from SQL, NoSQL, APIs, files, etc.
- **Complex Domain Logic**: If you're doing true DDD with aggregates and domain events
- **Microservices**: If services own their data and provide repositories as anti-corruption layers

**AONIK's Context**: None of these apply. We have:
- Single SQL database via EF Core
- Anemic domain model (see ADR 002)
- Monolithic architecture (for now)

## Alternative Patterns Available

If we need query reuse or abstraction, we can use:

1. **Extension Methods** (shown above) - Lightweight, composable
2. **Specification Pattern** - For complex, reusable query logic
3. **Query Objects** - For very complex multi-step queries
4. **CQRS Read Models** - For complex read scenarios (if needed later)

## Examples

### Good: Direct DbContext Usage

```csharp
public async Task<List<InvoiceResponse>> GetOverdueInvoicesAsync(CancellationToken ct = default)
{
    var invoices = await _dbContext.Invoices
        .Where(i => i.Status == "Issued")
        .Where(i => i.DueDate < DateTime.UtcNow)
        .Include(i => i.Lines)
        .OrderBy(i => i.DueDate)
        .ToListAsync(ct);
        
    return invoices.Select(MapToResponse).ToList();
}
```

### Good: Extension Method for Reusable Queries

```csharp
public static class InvoiceQueryExtensions
{
    public static IQueryable<Invoice> Overdue(this IQueryable<Invoice> query)
    {
        return query
            .Where(i => i.Status == "Issued")
            .Where(i => i.DueDate < DateTime.UtcNow);
    }
}

// Usage
var invoices = await _dbContext.Invoices
    .Overdue()
    .Include(i => i.Lines)
    .ToListAsync(ct);
```

### Avoid: Unnecessary Repository Abstraction

```csharp
// DON'T DO THIS - it's just wrapping EF Core with no value
public class InvoiceRepository : IInvoiceRepository
{
    private readonly IAonikDbContext _dbContext;
    
    public async Task<Invoice> GetByIdAsync(Guid id)
    {
        return await _dbContext.Invoices.FindAsync(id);
    }
    
    // ...more methods that just call DbSet methods
}
```

## References

- [Martin Fowler - Repository Pattern](https://martinfowler.com/eaaCatalog/repository.html)
- [Microsoft EF Core - DbContext Lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [Jimmy Bogard - Repository Anti-Pattern](https://lostechies.com/jimmybogard/2012/09/20/limiting-your-abstractions/)
- [Vladimir Khorikov - When to Use Repository Pattern](https://enterprisecraftsmanship.com/posts/repository-pattern-in-ef-core/)
- [AGENTS.md - Application Layer Patterns](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md#application-layer-patterns)

## Review

This decision should be revisited if:
- We adopt microservices and need cross-service data abstraction
- We introduce multiple data sources (SQL, NoSQL, external APIs) for the same entities
- We migrate to true DDD with rich domain models and aggregates (reversing ADR 002)
- EF Core becomes inadequate for our performance or scalability needs

**Next Review Date**: 2026-07-08 (6 months)
