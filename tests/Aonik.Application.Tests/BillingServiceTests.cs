using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Billing;
using Aonik.Application.Services.Billing;
using Aonik.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests;

public class BillingServiceTests
{
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
                new("Consulting Services", 10, 150.00m),
                new("Software License", 1, 500.00m)
            });

        // Act
        var result = await service.CreateInvoiceAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.InvoiceNumber.Should().Be("INV-001");
        result.Currency.Should().Be("USD");
        result.TotalAmount.Should().Be(2000.00m); // (10 * 150) + (1 * 500)
        result.LineItems.Should().HaveCount(2);
        result.Status.ToString().Should().Be("Draft");
    }

    [Fact]
    public async Task GetInvoiceAsync_ShouldReturnInvoice_WhenExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options);
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new BillingService(context, tenantProvider);

        var createRequest = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: "INV-002",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Product A", 2, 50.00m)
            });

        var created = await service.CreateInvoiceAsync(createRequest, CancellationToken.None);

        // Act
        var result = await service.GetInvoiceAsync(created.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.InvoiceNumber.Should().Be("INV-002");
    }

    [Fact]
    public async Task GetInvoiceAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options);
        var tenantProvider = new TestTenantProvider(Guid.NewGuid());
        var service = new BillingService(context, tenantProvider);

        // Act
        var result = await service.GetInvoiceAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
