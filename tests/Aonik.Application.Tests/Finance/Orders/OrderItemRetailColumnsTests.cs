using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance.Orders;

/// <summary>
/// Spec 041 / ADR-011 Phase 1: a product purchase is just another <see cref="OrderType"/>.
/// These tests lock in that the retail line columns added to <see cref="OrderItem"/>
/// (<c>Quantity</c>, <c>UnitPrice</c>, <c>ProductId</c>, <c>Sku</c>) persist and round-trip,
/// that the existing <c>AmountIn</c> carries the line total (Quantity × UnitPrice) with no
/// separate LineTotal column, and that the financial-only fields stay untouched for
/// non-product order types.
/// </summary>
public class OrderItemRetailColumnsTests
{
    private static DbContextOptions<FinanceDbContext> SharedOptions(string dbName)
        => new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

    private static FinanceDbContext CreateDbContext(DbContextOptions<FinanceDbContext> options, Guid tenantId)
        => new(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    [Fact]
    public async Task ProductPurchaseOrder_Should_PersistAndRoundTripRetailLineColumns()
    {
        // Arrange — a ProductPurchase order with two product lines. The line total lives on the
        // existing AmountIn (Quantity × UnitPrice); AmountOut / CurrencyOut / FxQuoteId stay unused.
        var tenantId = Guid.NewGuid();
        var dbName = $"OrderItemRetail_{Guid.NewGuid()}";
        var options = SharedOptions(dbName);

        var granolaProductId = Guid.NewGuid();
        var teaProductId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using (var context = CreateDbContext(options, tenantId))
        {
            var order = new Order
            {
                Id = orderId,
                TenantId = tenantId,
                OrderType = nameof(OrderType.ProductPurchase),
                Status = OrderStatuses.Draft,
                PayerPartyId = Guid.NewGuid(),
                CurrencyIn = "NGN",
                AmountIn = 14_500m // (3 × 4500) + (1 × 1000)
            };

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ItemType = nameof(OrderType.ProductPurchase),
                ItemIndex = 0,
                Status = "Valid",
                CurrencyIn = "NGN",
                CurrencyOut = "NGN",
                Quantity = 3m,
                UnitPrice = 4_500m,
                AmountIn = 13_500m, // line total = Quantity × UnitPrice
                ProductId = granolaProductId,
                Sku = "WELL-GRANOLA-500G"
            });

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ItemType = nameof(OrderType.ProductPurchase),
                ItemIndex = 1,
                Status = "Valid",
                CurrencyIn = "NGN",
                CurrencyOut = "NGN",
                Quantity = 1m,
                UnitPrice = 1_000m,
                AmountIn = 1_000m,
                ProductId = teaProductId,
                Sku = "WELL-TEA-20CT"
            });

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        // Act — read back through a fresh context so values come from the store, not the tracker.
        await using (var context = CreateDbContext(options, tenantId))
        {
            var reloaded = await context.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == orderId);

            // Assert
            reloaded.OrderType.Should().Be("ProductPurchase");
            reloaded.Items.Should().HaveCount(2);

            var granola = reloaded.Items.Single(i => i.ItemIndex == 0);
            granola.ProductId.Should().Be(granolaProductId);
            granola.Sku.Should().Be("WELL-GRANOLA-500G");
            granola.Quantity.Should().Be(3m);
            granola.UnitPrice.Should().Be(4_500m);
            granola.AmountIn.Should().Be(granola.Quantity * granola.UnitPrice);

            var tea = reloaded.Items.Single(i => i.ItemIndex == 1);
            tea.ProductId.Should().Be(teaProductId);
            tea.Sku.Should().Be("WELL-TEA-20CT");
            tea.AmountIn.Should().Be(tea.Quantity * tea.UnitPrice);

            reloaded.AmountIn.Should().Be(reloaded.Items.Sum(i => i.AmountIn));
        }
    }

    [Fact]
    public async Task FinancialOrderItem_Should_LeaveRetailColumnsNull()
    {
        // Arrange — a bill-payment line never sets the retail columns; they must round-trip as null.
        var tenantId = Guid.NewGuid();
        var dbName = $"OrderItemRetail_{Guid.NewGuid()}";
        var options = SharedOptions(dbName);
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using (var context = CreateDbContext(options, tenantId))
        {
            var order = new Order
            {
                Id = orderId,
                TenantId = tenantId,
                OrderType = nameof(OrderType.BillPayment),
                Status = OrderStatuses.Draft,
                CurrencyIn = "NGN",
                AmountIn = 5_000m
            };
            order.Items.Add(new OrderItem
            {
                Id = itemId,
                TenantId = tenantId,
                OrderId = orderId,
                ItemType = nameof(OrderType.BillPayment),
                ItemIndex = 0,
                Status = "Valid",
                CurrencyIn = "NGN",
                CurrencyOut = "NGN",
                AmountIn = 5_000m,
                AmountOut = 5_000m
            });
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        // Act / Assert
        await using (var context = CreateDbContext(options, tenantId))
        {
            var item = await context.Set<OrderItem>().SingleAsync(i => i.Id == itemId);

            item.Quantity.Should().BeNull();
            item.UnitPrice.Should().BeNull();
            item.ProductId.Should().BeNull();
            item.Sku.Should().BeNull();
        }
    }
}
