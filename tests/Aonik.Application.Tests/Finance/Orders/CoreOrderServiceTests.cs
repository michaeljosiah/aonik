using Aonik.Finance.Entities.Orders;
using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance.Orders;

/// <summary>
/// Spec 041 / ADR-011 Phase 2: the core, type-agnostic <see cref="IOrderService"/> contract,
/// implemented by <see cref="CoreOrderService"/>. Proves a ProductPurchase order flows through the
/// generic spine end-to-end (create → get → list → transition → fund) using the same machinery the
/// financial order types use.
/// </summary>
public class CoreOrderServiceTests
{
    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    }

    private static OrderingDbContext CreateDbContext(DbContextOptions<OrderingDbContext> options, Guid tenantId)
        => new(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    private static (DbContextOptions<OrderingDbContext> Options, Guid TenantId) NewDb()
        => (new DbContextOptionsBuilder<OrderingDbContext>()
                .UseInMemoryDatabase($"CoreOrder_{Guid.NewGuid()}").Options,
            Guid.NewGuid());

    private static CoreOrderService CreateService(OrderingDbContext context, Guid tenantId)
        => new(context, new TestTenantProvider(tenantId), new TestClock(), new TestCurrentUserProvider());

    private static CreateOrderCommand ProductPurchaseCommand(Guid? payerPartyId = null)
        => new(
            OrderType: OrderTypeCodes.ProductPurchase,
            PayerPartyId: payerPartyId ?? Guid.NewGuid(),
            CurrencyIn: "NGN",
            Items: new[]
            {
                new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 13_500m, "NGN",
                    Quantity: 3m, UnitPrice: 4_500m, ProductId: Guid.NewGuid(), Sku: "WELL-GRANOLA-500G"),
                new OrderItemCommand(OrderTypeCodes.ProductPurchase, 1, 1_000m, "NGN",
                    Quantity: 1m, UnitPrice: 1_000m, ProductId: Guid.NewGuid(), Sku: "WELL-TEA-20CT")
            });

    [Fact]
    public async Task CreateAsync_Should_PersistProductPurchaseOrder_WithRetailLines_AndDefaultTotal()
    {
        var (options, tenantId) = NewDb();

        Guid orderId;
        await using (var context = CreateDbContext(options, tenantId))
        {
            var service = CreateService(context, tenantId);

            // Act — no AmountIn supplied; it must default to the sum of the line totals.
            var created = await service.CreateAsync(ProductPurchaseCommand());

            // Assert
            created.OrderType.Should().Be("ProductPurchase");
            created.Status.Should().Be(OrderStatuses.Draft);
            created.AmountIn.Should().Be(14_500m);
            created.Items.Should().HaveCount(2);

            var granola = created.Items.Single(i => i.ItemIndex == 0);
            granola.Sku.Should().Be("WELL-GRANOLA-500G");
            granola.Quantity.Should().Be(3m);
            granola.UnitPrice.Should().Be(4_500m);
            granola.ProductId.Should().NotBeNull();

            orderId = created.Id;
        }

        // A fresh context proves it persisted, not just mapped from the tracker.
        await using (var context = CreateDbContext(options, tenantId))
        {
            var fetched = await CreateService(context, tenantId).GetAsync(orderId);
            fetched.Should().NotBeNull();
            fetched!.Items.Should().HaveCount(2);
            fetched.Items.Sum(i => i.AmountIn).Should().Be(14_500m);

            // Receiver/payer roles + a Created history event were written.
            (await context.OrderPartyRoles.CountAsync(r => r.OrderId == orderId)).Should().Be(1); // payer only (no receivers set)
            (await context.OrderHistoryEvents.CountAsync(e => e.OrderId == orderId && e.EventType == "Created"))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task TransitionAsync_Should_UpdateStatus_RecordHistory_AndBeIdempotentOnSameStatus()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        var created = await service.CreateAsync(ProductPurchaseCommand());

        var transitioned = await service.TransitionAsync(created.Id, OrderStatuses.Pending, "ready to pay");
        transitioned.Status.Should().Be(OrderStatuses.Pending);

        // Same-status transition is a no-op (no extra history row).
        await service.TransitionAsync(created.Id, OrderStatuses.Pending);

        (await context.OrderHistoryEvents.CountAsync(e => e.OrderId == created.Id && e.EventType == "StatusChanged"))
            .Should().Be(1);
    }

    [Fact]
    public async Task LinkFundingAsync_Should_AttachPaymentIntent()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        var created = await service.CreateAsync(ProductPurchaseCommand());
        var paymentIntentId = Guid.NewGuid();

        await service.LinkFundingAsync(created.Id, paymentIntentId);

        var funding = await context.OrderFundingRefs.SingleAsync(f => f.OrderId == created.Id);
        funding.PaymentIntentId.Should().Be(paymentIntentId);
    }

    [Fact]
    public async Task LinkFulfilmentAsync_Should_Reject_When_NotExactlyOneReferenceSet()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);
        var created = await service.CreateAsync(ProductPurchaseCommand());

        var act = async () => await service.LinkFulfilmentAsync(created.Id, new OrderFulfilmentLink());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListAsync_Should_FilterByOrderType_AndPage()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        await service.CreateAsync(ProductPurchaseCommand());
        await service.CreateAsync(ProductPurchaseCommand());
        await service.CreateAsync(new CreateOrderCommand(
            OrderTypeCodes.BillPayment, Guid.NewGuid(), "NGN",
            new[] { new OrderItemCommand(OrderTypeCodes.BillPayment, 0, 5_000m, "NGN") }));

        var products = await service.ListAsync(new ListOrdersQuery(OrderType: OrderTypeCodes.ProductPurchase));

        products.TotalCount.Should().Be(2);
        products.Items.Should().OnlyContain(o => o.OrderType == "ProductPurchase");
        products.Items.Should().OnlyContain(o => o.ItemCount == 2);
    }

    [Fact]
    public async Task CreateAsync_Should_ReturnExistingOrder_ForDuplicateIdempotencyKey()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        var command = ProductPurchaseCommand() with { IdempotencyKey = "order-123" };

        var first = await service.CreateAsync(command);
        var second = await service.CreateAsync(command);

        second.Id.Should().Be(first.Id);
        (await context.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_NormalizeIdempotencyKey_SoPaddedRetryHitsExistingOrder()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        // A retry whose key differs only by surrounding whitespace must resolve to the same order,
        // and the stored key is trimmed.
        var first = await service.CreateAsync(ProductPurchaseCommand() with { IdempotencyKey = "order-123" });
        var retry = await service.CreateAsync(ProductPurchaseCommand() with { IdempotencyKey = "  order-123  " });

        retry.Id.Should().Be(first.Id);
        (await context.Orders.CountAsync()).Should().Be(1);
        (await context.Orders.SingleAsync()).IdempotencyKey.Should().Be("order-123");
    }

    [Fact]
    public async Task CreateAsync_Should_StoreNullIdempotencyKey_ForBlankInput()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        // A blank key must be stored as NULL (exempt from the filtered unique index), so two blank-key
        // creates produce two distinct orders rather than colliding.
        await service.CreateAsync(ProductPurchaseCommand() with { IdempotencyKey = "   " });
        await service.CreateAsync(ProductPurchaseCommand() with { IdempotencyKey = "" });

        (await context.Orders.CountAsync()).Should().Be(2);
        (await context.Orders.CountAsync(o => o.IdempotencyKey == null)).Should().Be(2);
    }
}
