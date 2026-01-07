# AGENTS.md - Coding Guidelines for AONIK

This document provides coding standards, build commands, and architectural patterns for AI agents working in the AONIK codebase.

---

## 🔧 Build, Test & Development Commands

### Build Commands
```bash
# Build entire solution
dotnet build Aonik.sln

# Build specific project
dotnet build src/Aonik.Api
dotnet build src/Aonik.Application

# Clean build
dotnet clean Aonik.sln && dotnet build Aonik.sln
```

### Test Commands
```bash
# Run all tests
dotnet test Aonik.sln

# Run tests for a specific project
dotnet test tests/Aonik.Application.Tests
dotnet test tests/Aonik.Api.Tests

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~BillingServiceTests.CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems"
dotnet test --filter "DisplayName~CreateInvoice"

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run tests without building
dotnet test --no-build
```

### Database Commands
```bash
# Create migration
dotnet ef migrations add <MigrationName> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Update database
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Remove last migration
dotnet ef migrations remove --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

### Run API
```bash
dotnet run --project src/Aonik.Api
# API runs on https://localhost:5001 with Swagger UI at /swagger
```

---

## 📐 Architecture & Project Structure

### Clean Architecture Layers
- **SharedKernel**: Common primitives (Entity, Result<T>, Money, Guard)
- **Domain**: Business entities and logic (Invoice, LedgerAccount, PaymentIntent)
- **Application**: Services, DTOs, abstractions, AI workflows
- **Infrastructure**: EF Core, external services, AI providers
- **Api**: FastEndpoints HTTP endpoints
- **Worker**: Background jobs and scheduled tasks

### Module Organization
Code is organized by **business modules** (Ledger, Billing, Payments, AI) with vertical slices:
```
src/Aonik.Domain/Billing/Entities/Invoice.cs
src/Aonik.Application/Services/Billing/BillingService.cs
src/Aonik.Api/Endpoints/Billing/CreateInvoiceEndpoint.cs
```

---

## 🎨 Code Style Guidelines

### General Principles
- **Target Framework**: .NET 10 (`net10.0`)
- **Nullable Reference Types**: Enabled globally (use `string?` for nullable)
- **Implicit Usings**: Enabled (common namespaces auto-imported)
- **Language Version**: Latest C# features

### Naming Conventions
- **Classes/Interfaces**: PascalCase (`Invoice`, `IBillingService`)
- **Methods**: PascalCase (`CreateInvoiceAsync`)
- **Properties**: PascalCase (`CustomerId`, `InvoiceNumber`)
- **Private fields**: `_camelCase` with underscore prefix (`_dbContext`, `_lineItems`)
- **Parameters/locals**: camelCase (`customerId`, `invoiceNumber`)
- **Constants**: PascalCase (`PromptNames.InvoiceInsight`)
- **Async methods**: Suffix with `Async` (`GetInvoiceAsync`)

### File Organization
- **Namespace per file folder**: Match namespace to directory structure
- **One class per file**: Exception for small DTOs/records grouped logically
- **File naming**: Match primary class name (`Invoice.cs`, `BillingService.cs`)

### Import Order
1. System namespaces (`using System;`, `using System.Linq;`)
2. Third-party packages (`using Microsoft.EntityFrameworkCore;`, `using FastEndpoints;`)
3. Project namespaces (`using Aonik.Domain.Billing.Entities;`)
4. Blank line between groups

### Type Usage
- Prefer **explicit types** for clarity: `var` is acceptable when type is obvious from right side
- Use **records** for immutable DTOs: `public record CreateInvoiceRequest(...);`
- Use **nullable annotations**: `Invoice?` for potentially null references
- Prefer **async/await** over `.Result` or `.Wait()`
- Use **CancellationToken** parameters with default value: `CancellationToken cancellationToken = default`

---

## 🏗️ Domain & Entity Patterns

### Domain Entities
- Inherit from `Entity` base class (provides `Guid Id` and equality)
- Use **private setters** for properties: `public string Name { get; private set; }`
- Use **private parameterless constructor** for EF Core: `private Invoice() { }`
- Use **public constructor** with required parameters for domain creation
- Collections: Private `List<T>`, exposed as `IReadOnlyCollection<T>`
```csharp
private readonly List<InvoiceLineItem> _lineItems = new();
public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();
```
- Use **methods for state changes**: `MarkAsPaid()`, `AddLineItem()`, not property setters
- **Guard domain invariants** in methods with exceptions

### Value Objects
- Use **records** for immutability: `public record Money(decimal Amount, string Currency);`
- Override equality if needed (records have value equality by default)

---

## 📦 Application Layer Patterns

### Services
- Interface + implementation: `IBillingService` / `BillingService`
- Constructor injection: Inject `IAonikDbContext` or abstractions
- Return **DTOs**, not domain entities
- Use **async Task<T>** for all I/O operations
- Private mapping methods: `private static InvoiceResponse MapToResponse(Invoice invoice)`

### DTOs & Models
- Use **records** for request/response: `public record CreateInvoiceRequest(...);`
- Use **positional parameters**: `new InvoiceResponse(id, customerId, ...)`
- Located in `Application/Models/{Module}/` folders

---

## 🌐 API Layer (FastEndpoints)

### Endpoint Structure
- Inherit from `Endpoint<TRequest, TResponse>` or `EndpointWithoutRequest<TResponse>`
- Override `Configure()`: Set route with `Post("/billing/invoices")`, `AllowAnonymous()`
- Override `HandleAsync()`: Business logic, use `Send.*Async()` methods

### Response Methods
```csharp
await Send.OkAsync(response, ct);                           // 200 OK
await Send.CreatedAtAsync<GetEndpoint>(                     // 201 Created
    routeValues: new { id = response.Id }, 
    responseBody: response, 
    cancellation: ct);
await Send.NotFoundAsync(ct);                               // 404 Not Found
```
- **Never** use `SendAsync()`, `SendCreatedAsync()`, `ResponseAsync()` directly
- Map API contracts to Application DTOs in endpoint handlers

---

## 🧪 Testing Standards

### Test Structure (AAA Pattern)
```csharp
[Fact]
public async Task MethodName_Should_ExpectedBehavior_When_Condition()
{
    // Arrange
    var service = CreateService();
    
    // Act
    var result = await service.DoSomethingAsync();
    
    // Assert
    result.Should().NotBeNull();
}
```

### Assertions
- Use **FluentAssertions**: `.Should().Be()`, `.Should().HaveCount()`, `.Should().NotBeNull()`
- Avoid `Assert.Equal()` / `Assert.True()` from xUnit

### Database Tests
- Use **InMemory database** with unique name: `$"TestDb_{Guid.NewGuid()}"`
- Create fresh context per test: `using var context = new AonikDbContext(options);`

### API Integration Tests
- Infrastructure supports **environment-based database configuration**:
  - Set `UseInMemoryDatabase=true` in configuration to use InMemory database
  - Set `InMemoryDatabaseName` for custom database name
- API tests use `CustomWebApplicationFactory` with `ConfigureAppConfiguration()` to inject test configuration
- Example:
```csharp
builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["UseInMemoryDatabase"] = "true",
        ["InMemoryDatabaseName"] = "TestDb_" + Guid.NewGuid()
    });
});
```

---

## 🚨 Error Handling

- Use **exceptions** for exceptional cases (domain invariant violations)
- Return **null** for "not found" scenarios in queries
- Use **Result<T>** pattern from SharedKernel for operation outcomes (when implemented)
- Throw descriptive exceptions: `throw new InvalidOperationException("Only draft invoices can be issued");`

---

## 📝 Comments & Documentation

- Write **self-documenting code** with clear names
- Add comments only for **non-obvious business logic**
- No commented-out code in commits
- Use XML docs (`///`) for public APIs when helpful

---

## ✅ Pre-Commit Checklist

- [ ] `dotnet build Aonik.sln` succeeds
- [ ] `dotnet test Aonik.sln` passes
- [ ] No unused usings or variables
- [ ] Nullable annotations correct
- [ ] Async methods have `CancellationToken` parameter
- [ ] FastEndpoints use `Send.*Async()` methods correctly
- [ ] Domain entities maintain invariants
- [ ] Tests follow AAA pattern with FluentAssertions
