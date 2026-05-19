using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Finance.Readers;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance.Readers;

/// <summary>
/// Tests for <see cref="CustomerOrderHistoryReader"/> — the SharedKernel read contract
/// that lets PersonalFinance read order history without depending on
/// <c>Aonik.Finance.Entities.Orders</c> directly (Spec 027 Phase 0).
/// </summary>
public class CustomerOrderHistoryReaderTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid PartyA = Guid.NewGuid();
    private static readonly Guid PartyB = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => null;
        public bool TryGetCurrentUserId(out Guid userId) { userId = Guid.Empty; return false; }
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"OrderReaderTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider());
    }

    private static Order SeedOrder(
        Guid tenantId,
        decimal amountIn = 100m,
        string currencyIn = "USD",
        decimal? amountOut = null,
        string? currencyOut = null,
        string orderType = "BillPayment",
        string status = "Complete",
        DateTime? createdAt = null)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = orderType,
            AmountIn = amountIn,
            CurrencyIn = currencyIn,
            AmountOut = amountOut,
            CurrencyOut = currencyOut,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task GetForPartyAsync_Should_ReturnOrdersForParty_When_InsideWindow()
    {
        // Arrange
        using var db = CreateDbContext(TenantA);
        var order1 = SeedOrder(TenantA, amountIn: 50m, createdAt: DateTime.UtcNow.AddDays(-5));
        var order2 = SeedOrder(TenantA, amountIn: 75m, createdAt: DateTime.UtcNow.AddDays(-3));
        db.Orders.AddRange(order1, order2);
        db.OrderPartyRoles.AddRange(
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantA, OrderId = order1.Id, PartyId = PartyA, Role = "Payer" },
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantA, OrderId = order2.Id, PartyId = PartyA, Role = "Payer" });
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act
        var results = await reader.GetForPartyAsync(
            TenantA,
            PartyA,
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow);

        // Assert
        results.Should().HaveCount(2);
        results.Select(x => x.OrderId).Should().BeEquivalentTo([order1.Id, order2.Id]);
        results.Should().OnlyContain(x => x.CurrencyIn == "USD");
    }

    [Fact]
    public async Task GetForPartyAsync_Should_ExcludeOrdersOutsideWindow()
    {
        // Arrange
        using var db = CreateDbContext(TenantA);
        var inWindow = SeedOrder(TenantA);
        var outOfWindow = SeedOrder(TenantA);
        db.Orders.AddRange(inWindow, outOfWindow);
        db.OrderPartyRoles.AddRange(
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantA, OrderId = inWindow.Id, PartyId = PartyA, Role = "Payer" },
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantA, OrderId = outOfWindow.Id, PartyId = PartyA, Role = "Payer" });
        await db.SaveChangesAsync();

        // AonikDbContextBase.UpdateAuditFields overwrites CreatedAt on insert,
        // so we backdate the test fixtures after the save to land them inside
        // and outside the read window.
        inWindow.CreatedAt = DateTime.UtcNow.AddDays(-2);
        outOfWindow.CreatedAt = DateTime.UtcNow.AddDays(-30);
        // Skip audit-field reset on update — set state directly to Unchanged
        // first then mark only CreatedAt as modified.
        db.Entry(inWindow).Property(o => o.CreatedAt).IsModified = true;
        db.Entry(outOfWindow).Property(o => o.CreatedAt).IsModified = true;
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act
        var results = await reader.GetForPartyAsync(
            TenantA,
            PartyA,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow);

        // Assert
        results.Should().HaveCount(1);
        results[0].OrderId.Should().Be(inWindow.Id);
    }

    [Fact]
    public async Task GetForPartyAsync_Should_NotLeakAcrossTenants()
    {
        // Arrange — seed an order for TenantB but try to read it via TenantA scope.
        using var db = CreateDbContext(TenantA);
        var tenantBOrder = SeedOrder(TenantB);
        db.Orders.Add(tenantBOrder);
        db.OrderPartyRoles.Add(
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantB, OrderId = tenantBOrder.Id, PartyId = PartyA, Role = "Payer" });
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act
        var results = await reader.GetForPartyAsync(
            TenantA,
            PartyA,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow);

        // Assert — the TenantB row must NOT surface in a TenantA query.
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForPartyAsync_Should_OnlyMatchByPartyId()
    {
        // Arrange — orders linked to PartyB should not surface for a PartyA query.
        using var db = CreateDbContext(TenantA);
        var partyAOrder = SeedOrder(TenantA);
        var partyBOrder = SeedOrder(TenantA);
        db.Orders.AddRange(partyAOrder, partyBOrder);
        db.OrderPartyRoles.AddRange(
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantA, OrderId = partyAOrder.Id, PartyId = PartyA, Role = "Payer" },
            new OrderPartyRole { Id = Guid.NewGuid(), TenantId = TenantA, OrderId = partyBOrder.Id, PartyId = PartyB, Role = "Payer" });
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act
        var results = await reader.GetForPartyAsync(
            TenantA,
            PartyA,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow);

        // Assert
        results.Should().HaveCount(1);
        results[0].OrderId.Should().Be(partyAOrder.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_Should_ReturnRequestedOrders()
    {
        // Arrange
        using var db = CreateDbContext(TenantA);
        var order1 = SeedOrder(TenantA, amountIn: 200m);
        var order2 = SeedOrder(TenantA, amountIn: 300m);
        var order3 = SeedOrder(TenantA, amountIn: 400m);
        db.Orders.AddRange(order1, order2, order3);
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act — only request the first two.
        var results = await reader.GetByIdsAsync(TenantA, [order1.Id, order2.Id]);

        // Assert
        results.Should().HaveCount(2);
        results.Select(x => x.OrderId).Should().BeEquivalentTo([order1.Id, order2.Id]);
    }

    [Fact]
    public async Task GetByIdsAsync_Should_ReturnEmpty_When_NoIdsProvided()
    {
        using var db = CreateDbContext(TenantA);
        var reader = new CustomerOrderHistoryReader(db);

        var results = await reader.GetByIdsAsync(TenantA, []);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_Should_NotLeakAcrossTenants()
    {
        // Arrange — TenantB order known to caller, but caller scope is TenantA.
        using var db = CreateDbContext(TenantA);
        var tenantBOrder = SeedOrder(TenantB);
        db.Orders.Add(tenantBOrder);
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act
        var results = await reader.GetByIdsAsync(TenantA, [tenantBOrder.Id]);

        // Assert — caller knows the GUID but the tenant filter must still drop it.
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_Should_ProjectDualCurrencyFields()
    {
        // Arrange — explicitly set AmountOut/CurrencyOut to verify they reach the DTO.
        using var db = CreateDbContext(TenantA);
        var fxOrder = SeedOrder(
            TenantA,
            amountIn: 1000m,
            currencyIn: "GBP",
            amountOut: 1250m,
            currencyOut: "USD",
            orderType: "Remittance");
        db.Orders.Add(fxOrder);
        await db.SaveChangesAsync();

        var reader = new CustomerOrderHistoryReader(db);

        // Act
        var results = await reader.GetByIdsAsync(TenantA, [fxOrder.Id]);

        // Assert
        results.Should().HaveCount(1);
        var item = results[0];
        item.OrderType.Should().Be("Remittance");
        item.AmountIn.Should().Be(1000m);
        item.CurrencyIn.Should().Be("GBP");
        item.AmountOut.Should().Be(1250m);
        item.CurrencyOut.Should().Be("USD");
    }
}
