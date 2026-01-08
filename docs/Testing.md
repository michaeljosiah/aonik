# Testing Guide

This document provides guidelines and best practices for writing and running tests in the AONIK project.

## Table of Contents

- [Testing Philosophy](#testing-philosophy)
- [Test Structure](#test-structure)
- [Running Tests](#running-tests)
- [Writing Tests](#writing-tests)
- [Test Patterns](#test-patterns)
- [Common Pitfalls](#common-pitfalls)

---

## Testing Philosophy

AONIK follows these testing principles:

1. **Test business logic in services, not entities** - Since we use anemic domain entities, all business logic resides in application services
2. **Use AAA pattern** - Arrange, Act, Assert for clear test structure
3. **Each test should be independent** - No shared state between tests
4. **Use descriptive test names** - Format: `MethodName_Should_ExpectedBehavior_When_Condition`
5. **Prefer FluentAssertions** - More readable than xUnit's Assert methods

---

## Test Structure

The test projects mirror the main project structure:

```
tests/
├── Aonik.Domain.Tests/          # Domain entity tests (minimal due to anemic model)
├── Aonik.Application.Tests/     # Service layer tests (most business logic)
├── Aonik.Infrastructure.Tests/  # Infrastructure and persistence tests
└── Aonik.Api.Tests/            # API endpoint integration tests
```

---

## Running Tests

### All Tests

```bash
# Run all tests in the solution
dotnet test Aonik.sln

# Run all tests with detailed output
dotnet test Aonik.sln --logger "console;verbosity=detailed"

# Run tests without rebuilding
dotnet test Aonik.sln --no-build
```

### Specific Test Projects

```bash
# Run only application tests
dotnet test tests/Aonik.Application.Tests

# Run only API tests
dotnet test tests/Aonik.Api.Tests
```

### Filtering Tests

```bash
# Run a specific test by full name
dotnet test --filter "FullyQualifiedName~BillingServiceTests.CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~BillingServiceTests"

# Run tests by display name pattern
dotnet test --filter "DisplayName~CreateInvoice"

# Run tests by category (if using [Trait] attributes)
dotnet test --filter "Category=Integration"
```

---

## Writing Tests

### Application Service Tests

Application services require mocking of dependencies like `ITenantProvider`. Here's the pattern:

```csharp
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Services.Billing;
using Aonik.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests;

public class BillingServiceTests
{
    // Mock implementation for testing
    private class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options);
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new BillingService(context, tenantProvider);

        var request = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: "INV-001",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Consulting Services", 10, 150.00m)
            });

        // Act
        var result = await service.CreateInvoiceAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.InvoiceNumber.Should().Be("INV-001");
        result.Currency.Should().Be("USD");
        result.LineItems.Should().HaveCount(1);
    }
}
```

### Key Points for Service Tests:

1. **Use InMemory database** - Each test gets a unique database name to avoid conflicts
2. **Mock ITenantProvider** - Create a simple test implementation
3. **Dispose context properly** - Use `using` statement
4. **Test the response DTO** - Services return DTOs, not entities
5. **Use FluentAssertions** - `.Should().Be()`, `.Should().HaveCount()`, etc.

---

## Test Patterns

### Testing with Anemic Entities

Since entities are anemic (no behavior methods), manipulate properties directly:

```csharp
// ❌ DON'T: Try to call behavior methods (they don't exist)
payment.Authorize();

// ✅ DO: Set properties directly
payment.Status = "Authorized";
await context.SaveChangesAsync();
```

### Testing Date/Time Values

```csharp
// Use BeCloseTo for datetime comparisons
result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
```

### Testing Collections

```csharp
// Check collection count
result.LineItems.Should().HaveCount(3);

// Check collection contents
result.LineItems.Should().Contain(item => item.Description == "Test Item");
```

### Testing Exceptions

```csharp
// Test that an exception is thrown
var act = async () => await service.DeleteInvoiceAsync(invalidId);

act.Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("Invoice not found");
```

### API Integration Tests

API tests use `CustomWebApplicationFactory` to configure the test environment:

```csharp
public class InvoiceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InvoiceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateInvoice_ReturnsCreated()
    {
        // Arrange
        var request = new { /* ... */ };

        // Act
        var response = await _client.PostAsJsonAsync("/billing/invoices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## Common Pitfalls

### 1. Forgetting to Mock ITenantProvider

**Error**: `There is no argument given that corresponds to the required parameter 'tenantProvider'`

**Solution**: Always create a `TestTenantProvider` and pass it to service constructors.

### 2. Referencing Non-Existent Entity Properties

**Error**: `'Invoice' does not contain a definition for 'InvoiceNumber'`

**Solution**: Check the actual entity definition. The entity might have different property names than expected.

### 3. Trying to Call Behavior Methods on Entities

**Error**: `'PaymentIntent' does not contain a definition for 'Authorize'`

**Solution**: Remember that entities are anemic. Set properties directly instead of calling methods.

### 4. Shared Database State Between Tests

**Problem**: Tests fail when run together but pass when run individually

**Solution**: Use unique database names for each test:
```csharp
.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
```

### 5. Not Disposing Context

**Problem**: Resource leaks or locks

**Solution**: Always use `using` statement:
```csharp
using var context = new AonikDbContext(options);
```

---

## Test Configuration

### InMemory Database vs SQL Server

By default, tests use InMemory database for speed. The infrastructure supports switching via configuration:

```csharp
// InMemory (default for tests)
builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["UseInMemoryDatabase"] = "true",
        ["InMemoryDatabaseName"] = "TestDb_" + Guid.NewGuid()
    });
});

// SQL Server (for integration tests if needed)
builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["UseInMemoryDatabase"] = "false",
        ["ConnectionStrings:DefaultConnection"] = "your-connection-string"
    });
});
```

---

## Best Practices Summary

1. ✅ Use AAA pattern (Arrange, Act, Assert)
2. ✅ Use FluentAssertions for readable assertions
3. ✅ Mock ITenantProvider for service tests
4. ✅ Use unique InMemory database per test
5. ✅ Dispose contexts with `using` statements
6. ✅ Test services, not entities (anemic model)
7. ✅ Use descriptive test names
8. ✅ Keep tests independent and isolated
9. ✅ Test the response DTOs, not raw entities
10. ✅ Use `.Should().BeCloseTo()` for datetime comparisons

---

## Further Reading

- **[AGENTS.md](../AGENTS.md)** - Coding standards and patterns
- **[FluentAssertions Documentation](https://fluentassertions.com/)**
- **[xUnit Documentation](https://xunit.net/)**
- **[EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)**
