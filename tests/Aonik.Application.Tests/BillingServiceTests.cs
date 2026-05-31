using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
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

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId) => _userId = userId;

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new FinanceDbContext(options, new TestTenantProvider(tenantId));
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new BillingService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context));

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
        result.InvoiceNumber.Should().NotBeNullOrEmpty();
        result.Currency.Should().Be("USD");
        result.TotalAmount.Should().Be(2000.00m); // (10 * 150) + (1 * 500)
        result.LineItems.Should().HaveCount(2);
        result.Status.ToString().Should().Be("Draft");
    }

    [Fact]
    public async Task GetInvoiceAsync_ShouldReturnInvoice_WhenExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new FinanceDbContext(options, new TestTenantProvider(tenantId));
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new BillingService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context));

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
        result.InvoiceNumber.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetInvoiceAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new FinanceDbContext(options, new TestTenantProvider(tenantId));
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new BillingService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context));

        // Act
        var result = await service.GetInvoiceAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
