using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using OrderEntity = Aonik.Finance.Entities.Orders.Order;

namespace Aonik.Application.Tests.Platform;

/// <summary>Spec 080 — the registry read-model's two cross-module pieces: the spine-wide
/// per-party order aggregate, and the domain-contributor contract Platform aggregates
/// without knowing any module's tables.</summary>
public class CustomerRegistryReadModelTests
{
    private static (CoreOrderService Orders, Guid TenantId, string Db) NewSpine()
    {
        var tenantId = Guid.NewGuid();
        var db = $"OrderingDb_{Guid.NewGuid()}";
        var tenant = new TestTenantProvider(tenantId);
        var ctx = new OrderingDbContext(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(db).Options,
            tenant, new TestCurrentUserProvider());
        return (new CoreOrderService(ctx, tenant, new FixedClock(), new TestCurrentUserProvider()), tenantId, db);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    }

    private static OrderEntity Order(Guid tenantId, Guid? payer, string type, decimal amount, string currency) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OrderType = type,
        PayerPartyId = payer,
        AmountIn = amount,
        CurrencyIn = currency,
        Status = OrderStatusCodes.Pending,
        ProvenanceJson = "{}",
    };

    [Fact]
    public async Task PartyOrderAggregates_AreSpineWide_AndNeverSumAcrossCurrencies()
    {
        var (orders, tenantId, db) = NewSpine();
        var tenant = new TestTenantProvider(tenantId);
        var buyer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var noOrders = Guid.NewGuid();

        await using (var seed = new OrderingDbContext(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(db).Options,
            tenant, new TestCurrentUserProvider()))
        {
            // Deliberately MIXED order types and currencies for one payer: ADR-011 says there is
            // one spine, so a registry that counted only the storefront would understate them.
            seed.Orders.AddRange(
                Order(tenantId, buyer, "ProductPurchase", 95m, "GBP"),
                Order(tenantId, buyer, "BillPayment", 40m, "GBP"),
                Order(tenantId, buyer, "Remittance", 90_000m, "NGN"),
                Order(tenantId, other, "ProductPurchase", 10m, "GBP"),
                Order(tenantId, null, "ProductPurchase", 999m, "GBP"));   // unattributed
            await seed.SaveChangesAsync();
        }

        var result = await orders.GetPartyOrderAggregatesAsync([buyer, other, noOrders]);

        result.Should().ContainKey(noOrders);
        result[noOrders].Should().Be(PartyOrderAggregate.Empty, "a party with no orders reads as an explicit zero");

        var mine = result[buyer];
        mine.OrderCount.Should().Be(3, "every OrderType counts — one spine");
        mine.TotalByCurrency.Should().HaveCount(2, "GBP and NGN are never added together");
        mine.TotalByCurrency.Select(t => t.Currency).Should().ContainInOrder("GBP", "NGN");
        mine.TotalByCurrency.Single(t => t.Currency == "GBP").Amount.Should().Be(135m);
        mine.TotalByCurrency.Single(t => t.Currency == "NGN").Amount.Should().Be(90_000m);

        result[other].OrderCount.Should().Be(1, "another payer's orders never leak into this one");
    }

    [Fact]
    public async Task PartyOrderAggregates_AreTenantScoped()
    {
        var (orders, tenantId, db) = NewSpine();
        var tenant = new TestTenantProvider(tenantId);
        var shared = Guid.NewGuid();

        await using (var seed = new OrderingDbContext(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(db).Options,
            tenant, new TestCurrentUserProvider()))
        {
            seed.Orders.Add(Order(tenantId, shared, "ProductPurchase", 50m, "GBP"));
            await seed.SaveChangesAsync();
        }

        // The SAME party id transacting under a different tenant must not be visible here.
        var foreignTenant = Guid.NewGuid();
        await using (var seed = new OrderingDbContext(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(db).Options,
            new TestTenantProvider(foreignTenant), new TestCurrentUserProvider()))
        {
            seed.Orders.Add(Order(foreignTenant, shared, "ProductPurchase", 7_000m, "GBP"));
            await seed.SaveChangesAsync();
        }

        var result = await orders.GetPartyOrderAggregatesAsync([shared]);
        result[shared].OrderCount.Should().Be(1);
        result[shared].TotalByCurrency.Single().Amount.Should().Be(50m, "the other tenant's order is invisible");
    }

    [Fact]
    public async Task PartyOrderAggregates_HandleTheEmptyRequest()
    {
        var (orders, _, _) = NewSpine();
        (await orders.GetPartyOrderAggregatesAsync([])).Should().BeEmpty();
    }

    [Fact]
    public async Task DomainKeys_AreStableContractValues()
    {
        // These strings are serialized to the client as chips and accepted back as the domain=
        // filter, so renaming one silently breaks saved views — pin them.
        CustomerRegistryDomains.Billing.Should().Be("billing");
        CustomerRegistryDomains.Storefront.Should().Be("storefront");
        CustomerRegistryDomains.PersonalFinance.Should().Be("personal-finance");
        await Task.CompletedTask;
    }
}
