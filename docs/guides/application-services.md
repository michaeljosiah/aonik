# Application Services Guidelines

## Overview

In AONIK, **Application Services** contain all business logic and orchestrate domain operations. Since we use an **anemic domain model**, entities are simple data containers, and services handle all state transitions, validations, and business rules.

Services live within their owning module project (e.g., `src/Aonik.Finance/Services/Billing/`).

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
    private readonly FinanceDbContext _dbContext;

    public BillingService(FinanceDbContext dbContext)
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

Services use the module-scoped DbContext directly:

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

- Validate business rules
- Orchestrate domain operations
- Coordinate transactions (implicit via `SaveChangesAsync`)
- Map between entities and DTOs

### What Services SHOULD NOT Do

- Direct HTTP concerns (that's for endpoints)
- Infrastructure details (that's for external adapters)
- UI logic (that's for clients)
- Entity construction with complex logic (entities are data bags)

## Location

Services are organized by subdomain within their owning module:

```
src/Aonik.Finance/Services/
├── Billing/
│   ├── IBillingService.cs
│   └── BillingService.cs
├── Payments/
│   ├── IPaymentService.cs
│   └── PaymentService.cs
└── Ledger/
    ├── ILedgerService.cs
    └── LedgerService.cs

src/Aonik.Platform/Services/
├── Identity/
├── Party/
└── Settings/
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

Services should be testable with in-memory database. See [Testing Guide](../Testing.md) for patterns and examples.

## Summary

- Put all business logic in services
- Use the module-scoped DbContext directly (no generic repository)
- Return DTOs, not entities
- Use async/await with CancellationToken
- Validate business rules and throw exceptions
- Map entities to DTOs in private methods
