:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Application Services Guidelines

## Overview

In AONIK, **Application Services** contain all business logic and orchestrate domain operations. Since we use an **anemic domain model**, entities are simple data containers, and services handle all state transitions, validations, and business rules.

## Service Structure

### Basic Pattern

```csharp
public interface IBillingService
{
    Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}

public class BillingService : IBillingService
{
    private readonly IAonikDbContext _dbContext;

    public BillingService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Implementation...
}
```

## Key Principles

### 1. **All Business Logic in Services**

Since entities are anemic (data-only), ALL business logic lives in services:

```csharp
public async Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
{
    var invoice = await _dbContext.Invoices
        .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    if (invoice == null)
        throw new InvalidOperationException($"Invoice {invoiceId} not found");

    // Business logic: Validate state transition
    if (invoice.Status != "Draft")
        throw new InvalidOperationException("Only draft invoices can be issued");

    // Business logic: Change state
    invoice.Status = "Issued";
    invoice.IssueDate = DateTime.UtcNow;

    await _dbContext.SaveChangesAsync(cancellationToken);
}
```

### 2. **Direct DbContext Usage (No Generic Repository)**

Services use `IAonikDbContext` directly:

```csharp
// ✅ Good - Direct DbContext usage
var invoices = await _dbContext.Invoices
    .Include(i => i.Lines)
    .Where(i => i.Status == "Draft")
    .ToListAsync(cancellationToken);

// ❌ Avoid - Generic repository adds unnecessary abstraction
var invoices = await _repository.GetAllAsync<Invoice>(
    i => i.Status == "Draft", 
    include: q => q.Include(i => i.Lines));
```

### 3. **Return DTOs, Not Entities**

Always map entities to DTOs before returning:

```csharp
public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
{
    var invoice = await _dbContext.Invoices
        .Include(i => i.Lines)
        .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    return invoice == null ? null : MapToResponse(invoice);
}

private static InvoiceResponse MapToResponse(Invoice invoice)
{
    return new InvoiceResponse(
        invoice.Id,
        invoice.CustomerAccountId,
        invoice.Currency,
        invoice.Total,
        invoice.Status,
        invoice.Lines.Select(l => new InvoiceLineResponse(
            l.Id,
            l.Description,
            l.Quantity,
            l.UnitPrice,
            l.LineTotal)).ToList());
}
```

### 4. **Use Async/Await Throughout**

All I/O operations should be async:

```csharp
// ✅ Good
public async Task<List<InvoiceResponse>> GetOverdueInvoicesAsync(CancellationToken cancellationToken = default)
{
    var invoices = await _dbContext.Invoices
        .Where(i => i.Status == "Issued" && i.DueDate < DateTime.UtcNow)
        .ToListAsync(cancellationToken);

    return invoices.Select(MapToResponse).ToList();
}

// ❌ Bad - Blocking
public List<InvoiceResponse> GetOverdueInvoices()
{
    var invoices = _dbContext.Invoices
        .Where(i => i.Status == "Issued" && i.DueDate < DateTime.UtcNow)
        .ToList(); // Blocking!

    return invoices.Select(MapToResponse).ToList();
}
```

### 5. **CancellationToken Parameter**

Always include `CancellationToken` with default value:

```csharp
public async Task<InvoiceResponse> CreateInvoiceAsync(
    CreateInvoiceRequest request, 
    CancellationToken cancellationToken = default)
{
    // Implementation...
}
```

## Service Responsibilities

### What Services SHOULD Do

✅ **Validate business rules**
```csharp
if (invoice.Status == "Paid")
    throw new InvalidOperationException("Paid invoices cannot be cancelled");
```

✅ **Orchestrate domain operations**
```csharp
// Create invoice
var invoice = new Invoice { /* properties */ };

// Add lines
foreach (var lineRequest in request.LineItems)
{
    var line = new InvoiceLine { /* properties */ };
    invoice.Lines.Add(line);
}

// Recalculate totals
RecalculateInvoiceTotals(invoice);
```

✅ **Coordinate transactions**
```csharp
// All operations in one transaction (implicit via SaveChangesAsync)
_dbContext.Invoices.Add(invoice);
_dbContext.Ledger.Add(journalEntry);
await _dbContext.SaveChangesAsync(cancellationToken);
```

✅ **Map between entities and DTOs**
```csharp
private static InvoiceResponse MapToResponse(Invoice invoice)
{
    // Mapping logic
}
```

### What Services SHOULD NOT Do

❌ **Direct HTTP concerns** (that's for endpoints)
❌ **Infrastructure details** (that's for Infrastructure layer)
❌ **UI logic** (that's for clients)
❌ **Entity construction with complex logic** (entities are data bags)

## Common Patterns

### Pattern 1: Create Entity

```csharp
public async Task<InvoiceResponse> CreateInvoiceAsync(
    CreateInvoiceRequest request, 
    CancellationToken cancellationToken = default)
{
    // 1. Create entity using object initializer
    var invoice = new Invoice
    {
        Id = Guid.NewGuid(),
        InvoiceId = Guid.NewGuid(),
        TenantId = Guid.Empty, // Get from context
        CustomerAccountId = request.CustomerId,
        Currency = request.Currency,
        DueDate = request.DueDate,
        Status = "Draft",
        Subtotal = 0,
        Total = 0,
        Lines = new List<InvoiceLine>()
    };

    // 2. Add child entities
    foreach (var lineRequest in request.LineItems)
    {
        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = lineRequest.Description,
            Quantity = lineRequest.Quantity,
            UnitPrice = lineRequest.UnitPrice,
            LineTotal = lineRequest.Quantity * lineRequest.UnitPrice
        };
        invoice.Lines.Add(line);
    }

    // 3. Perform calculations
    RecalculateInvoiceTotals(invoice);

    // 4. Save
    _dbContext.Invoices.Add(invoice);
    await _dbContext.SaveChangesAsync(cancellationToken);

    // 5. Return DTO
    return MapToResponse(invoice);
}
```

### Pattern 2: Update Entity

```csharp
public async Task UpdateInvoiceAsync(
    Guid invoiceId, 
    UpdateInvoiceRequest request, 
    CancellationToken cancellationToken = default)
{
    // 1. Load entity
    var invoice = await _dbContext.Invoices
        .Include(i => i.Lines)
        .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    if (invoice == null)
        throw new InvalidOperationException($"Invoice {invoiceId} not found");

    // 2. Validate business rules
    if (invoice.Status != "Draft")
        throw new InvalidOperationException("Only draft invoices can be updated");

    // 3. Update properties directly
    invoice.DueDate = request.DueDate;
    invoice.Currency = request.Currency;

    // 4. Save (EF tracks changes automatically)
    await _dbContext.SaveChangesAsync(cancellationToken);
}
```

### Pattern 3: State Transition

```csharp
public async Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
{
    var invoice = await _dbContext.Invoices
        .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    if (invoice == null)
        throw new InvalidOperationException($"Invoice {invoiceId} not found");

    // Validate state transition
    if (invoice.Status != "Draft")
        throw new InvalidOperationException("Only draft invoices can be issued");

    // Change state
    invoice.Status = "Issued";
    invoice.IssueDate = DateTime.UtcNow;

    await _dbContext.SaveChangesAsync(cancellationToken);
}
```

### Pattern 4: Complex Calculations

```csharp
private static void RecalculateInvoiceTotals(Invoice invoice)
{
    invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
    invoice.TaxTotal = invoice.Lines.Sum(x => x.LineTotal * x.TaxRate);
    invoice.Total = invoice.Subtotal + invoice.TaxTotal - invoice.DiscountTotal;
}
```

## Error Handling

### Use Exceptions for Business Rule Violations

```csharp
// ✅ Good - Clear business rule violation
if (invoice.Status == "Paid")
    throw new InvalidOperationException("Paid invoices cannot be cancelled");

// ❌ Bad - Silent failure
if (invoice.Status == "Paid")
    return null;
```

### Return Null for "Not Found" Scenarios

```csharp
// ✅ Good - Null indicates "not found"
public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
{
    var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
    return invoice == null ? null : MapToResponse(invoice);
}
```

## Testing Services

Services should be testable with in-memory database:

```csharp
[Fact]
public async Task CreateInvoiceAsync_ShouldCreateInvoiceWithLines()
{
    // Arrange
    var options = new DbContextOptionsBuilder<AonikDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;
    
    using var context = new AonikDbContext(options);
    var service = new BillingService(context);

    var request = new CreateInvoiceRequest(
        CustomerId: Guid.NewGuid(),
        Currency: "USD",
        DueDate: DateTime.UtcNow.AddDays(30),
        LineItems: new List<CreateInvoiceLineItemRequest>
        {
            new("Item 1", 2, 100.00m),
            new("Item 2", 1, 50.00m)
        });

    // Act
    var response = await service.CreateInvoiceAsync(request);

    // Assert
    response.Should().NotBeNull();
    response.Total.Should().Be(250.00m);
    response.LineItems.Should().HaveCount(2);
}
```

## Location

Services should be organized by business module:

```
src/Aonik.Application/Services/
├── Billing/
│   ├── IBillingService.cs
│   └── BillingService.cs
├── Payments/
│   ├── IPaymentService.cs
│   └── PaymentService.cs
└── Ledger/
    ├── ILedgerService.cs
    └── LedgerService.cs
```

## Summary

✅ **DO:**
- Put all business logic in services
- Use DbContext directly (no generic repository)
- Return DTOs, not entities
- Use async/await with CancellationToken
- Validate business rules and throw exceptions
- Map entities to DTOs in private methods

❌ **DON'T:**
- Put business logic in entities (anemic model)
- Use generic repositories
- Return entities from services
- Use blocking I/O
- Silently fail validation
- Mix service logic with HTTP concerns
