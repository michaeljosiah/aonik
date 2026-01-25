using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Orders;
using Aonik.Application.Models.Pricing;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Orders;
using Aonik.Infrastructure;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Orders;

public class OrderServiceTests
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
    public async Task CreateAsync_Should_CreateIssuedOrder_WithPricingMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        var service = CreateService(context, tenantId);

        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            OrderType: "BillPayment",
            ServiceCode: "BILLPAY",
            Amount: 12000m,
            Currency: "KES",
            PricingQuoteId: Guid.NewGuid(),
            ExchangeRate: 150.22m,
            RateMarkup: 0.015m,
            FeesTotal: 4.0m,
            TotalAmount: 12004m,
            FeeBreakdown: Array.Empty<FeeBreakdownItem>(),
            Payer: null,
            Payee: null,
            Details: new OrderDetails(
                BillPayment: new BillPaymentDetails(
                    BillerId: Guid.NewGuid(),
                    BillReference: "ACC-00991234",
                    BillerAccountId: null,
                    BillerCategory: null,
                    BillerCountry: null),
                BankTransfer: null,
                CashCollection: null),
            Items: null,
            Metadata: null);

        // Act
        var response = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        response.OrderId.Should().NotBe(Guid.Empty);
        response.OrderNumber.Should().NotBeNullOrWhiteSpace();
        response.InvoiceId.Should().NotBeNull();
        response.Status.Should().Be(Aonik.Domain.Orders.OrderStatuses.Pending);
        response.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task ValidateDuplicateAsync_Should_ReturnExistingOrder_When_DuplicateFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var billerId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        var service = CreateService(context, tenantId);

        var details = new OrderDetails(
            BillPayment: new BillPaymentDetails(
                BillerId: billerId,
                BillReference: "ACC-00991234",
                BillerAccountId: null,
                BillerCategory: null,
                BillerCountry: null),
            BankTransfer: null,
            CashCollection: null);

        var createRequest = new CreateOrderRequest(
            CustomerId: customerId,
            OrderType: "BillPayment",
            ServiceCode: "BILLPAY",
            Amount: 12000m,
            Currency: "KES",
            PricingQuoteId: Guid.NewGuid(),
            ExchangeRate: null,
            RateMarkup: null,
            FeesTotal: null,
            TotalAmount: null,
            FeeBreakdown: null,
            Payer: null,
            Payee: null,
            Details: details,
            Items: null,
            Metadata: null);

        var created = await service.CreateAsync(createRequest, CancellationToken.None);

        var validateRequest = new ValidateDuplicateOrderRequest(
            CustomerId: customerId,
            OrderType: "BillPayment",
            ServiceCode: "BILLPAY",
            Amount: 12000m,
            Currency: "KES",
            Details: details,
            RequestedAt: DateTimeOffset.UtcNow);

        // Act
        var response = await service.ValidateDuplicateAsync(validateRequest, CancellationToken.None);

        // Assert
        response.OrderId.Should().Be(created.OrderId);
        response.OrderNumber.Should().Be(created.OrderNumber);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_BillReferenceMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        var service = CreateService(context, tenantId);

        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            OrderType: "BillPayment",
            ServiceCode: "BILLPAY",
            Amount: 12000m,
            Currency: "KES",
            PricingQuoteId: Guid.NewGuid(),
            ExchangeRate: null,
            RateMarkup: null,
            FeesTotal: null,
            TotalAmount: null,
            FeeBreakdown: null,
            Payer: null,
            Payee: null,
            Details: new OrderDetails(
                BillPayment: new BillPaymentDetails(
                    BillerId: Guid.NewGuid(),
                    BillReference: " ",
                    BillerAccountId: null,
                    BillerCategory: null,
                    BillerCountry: null),
                BankTransfer: null,
                CashCollection: null),
            Items: null,
            Metadata: null);

        // Act
        var act = async () => await service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_DestinationAccountMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        var service = CreateService(context, tenantId);

        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            OrderType: "BankTransfer",
            ServiceCode: "BANK",
            Amount: 12000m,
            Currency: "KES",
            PricingQuoteId: Guid.NewGuid(),
            ExchangeRate: null,
            RateMarkup: null,
            FeesTotal: null,
            TotalAmount: null,
            FeeBreakdown: null,
            Payer: null,
            Payee: null,
            Details: new OrderDetails(
                BillPayment: null,
                BankTransfer: new BankTransferDetails(
                    DestinationAccountId: null,
                    DestinationAccountNumber: " ",
                    DestinationBankCode: "BANK01",
                    DestinationCountry: "KE",
                    Purpose: null),
                CashCollection: null),
            Items: null,
            Metadata: null);

        // Act
        var act = async () => await service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_RecipientMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        var service = CreateService(context, tenantId);

        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            OrderType: "CashCollection",
            ServiceCode: "CASH",
            Amount: 12000m,
            Currency: "KES",
            PricingQuoteId: Guid.NewGuid(),
            ExchangeRate: null,
            RateMarkup: null,
            FeesTotal: null,
            TotalAmount: null,
            FeeBreakdown: null,
            Payer: null,
            Payee: null,
            Details: new OrderDetails(
                BillPayment: null,
                BankTransfer: null,
                CashCollection: new CashCollectionDetails(
                    RecipientId: Guid.Empty,
                    PickupLocation: null,
                    PickupToken: null,
                    SenderId: null)),
            Items: null,
            Metadata: null);

        // Act
        var act = async () => await service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static OrderService CreateService(AonikDbContext context, Guid tenantId)
    {
        return new OrderService(
            context,
            new TestTenantProvider(tenantId),
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new SystemTextJsonSerializer());
    }
}
