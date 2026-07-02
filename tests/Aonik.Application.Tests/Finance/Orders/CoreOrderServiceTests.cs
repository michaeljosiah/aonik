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

    /// <summary>A settable clock, passed into the DbContext so audit stamping (Order.CreatedAt —
    /// the Spec 055 created-range filter field) is test-controlled instead of wall-clock.</summary>
    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    }

    private static OrderingDbContext CreateDbContext(DbContextOptions<OrderingDbContext> options, Guid tenantId, IClock? clock = null)
        => new(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), clock);

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
    public async Task ListAsync_Should_FilterByCreatedRange_HalfOpen()
    {
        // Spec 055 §9 — CreatedFromUtc is INCLUSIVE, CreatedToUtc EXCLUSIVE, so adjacent windows
        // never double-count a boundary order.
        var (options, tenantId) = NewDb();
        var clock = new MutableClock();
        await using var context = CreateDbContext(options, tenantId, clock);
        var service = CreateService(context, tenantId);

        var fromUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);

        clock.UtcNow = fromUtc.AddDays(-1);
        await service.CreateAsync(ProductPurchaseCommand());          // before the window
        clock.UtcNow = fromUtc;
        var atFrom = await service.CreateAsync(ProductPurchaseCommand());   // ON the lower bound — counts
        clock.UtcNow = fromUtc.AddDays(3);
        var inside = await service.CreateAsync(ProductPurchaseCommand());   // inside — counts
        clock.UtcNow = toUtc;
        await service.CreateAsync(ProductPurchaseCommand());          // ON the upper bound — excluded

        var page = await service.ListAsync(new ListOrdersQuery(CreatedFromUtc: fromUtc, CreatedToUtc: toUtc));

        page.TotalCount.Should().Be(2);
        page.Items.Select(o => o.Id).Should().BeEquivalentTo(new[] { atFrom.Id, inside.Id });
    }

    [Fact]
    public async Task ListAsync_AndListWithItemsAsync_Should_PageDeterministically_When_CreatedAtTies()
    {
        // CreatedAt alone is not a total order: orders created in the same instant leave the
        // database free to reorder ties between page queries, so a multi-page window walk could
        // skip or double-count an order. Both list paths pin (CreatedAt DESC, Id ASC), so pages
        // tile the window exactly — every order once, no duplicates, stable across calls.
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId, new TestClock()); // fixed clock — every order gets the identical CreatedAt
        var service = CreateService(context, tenantId);

        var created = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            created.Add((await service.CreateAsync(ProductPurchaseCommand())).Id);
        }
        // All CreatedAt equal → the Id tie-breaker alone defines the total order.
        var expectedOrder = created.OrderBy(id => id).ToList();

        var summaryWalk = new List<Guid>();
        var fullWalk = new List<Guid>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var summaries = await service.ListAsync(new ListOrdersQuery(PageNumber: pageNumber, PageSize: 2));
            summaryWalk.AddRange(summaries.Items.Select(o => o.Id));

            var fulls = await service.ListWithItemsAsync(new ListOrdersQuery(PageNumber: pageNumber, PageSize: 2));
            fullWalk.AddRange(fulls.Items.Select(o => o.Id));
        }

        // Union of the pages = all five orders, in the pinned order — no skips, no double-counts.
        summaryWalk.Should().Equal(expectedOrder);
        fullWalk.Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task ListWithItemsAsync_Should_ReturnFullOrders_WithLineItems_UnderTheSameFilters()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        await service.CreateAsync(ProductPurchaseCommand());
        await service.CreateAsync(new CreateOrderCommand(
            OrderTypeCodes.BillPayment, Guid.NewGuid(), "NGN",
            new[] { new OrderItemCommand(OrderTypeCodes.BillPayment, 0, 5_000m, "NGN") }));

        var page = await service.ListWithItemsAsync(new ListOrdersQuery(OrderType: OrderTypeCodes.ProductPurchase));

        // Full OrderDto per order — the per-line retail fields the production sheet aggregates
        // (Quantity, ProductId) are present, which OrderSummary deliberately omits.
        page.TotalCount.Should().Be(1);
        var order = page.Items.Single();
        order.OrderType.Should().Be(OrderTypeCodes.ProductPurchase);
        order.Items.Should().HaveCount(2);
        order.Items.Should().OnlyContain(i => i.Quantity != null && i.ProductId != null);
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
    public async Task CreateAsync_Should_MaterializeSuppliedPartyRoles_AndDedupeAgainstAutoRoles()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        // Spec 053 §10 — the additive PartyRoles hook: a supplied Supplier role is persisted
        // alongside the auto-materialized Payer role; an entry duplicating the Payer (same party,
        // same role) and a duplicate of a supplied entry are deduped, not double-inserted.
        var payerPartyId = Guid.NewGuid();
        var supplierPartyId = Guid.NewGuid();

        var created = await service.CreateAsync(ProductPurchaseCommand(payerPartyId) with
        {
            PartyRoles = new[]
            {
                new OrderPartyRoleCommand(supplierPartyId, OrderPartyRoleCodes.Supplier),
                new OrderPartyRoleCommand(supplierPartyId, OrderPartyRoleCodes.Supplier), // duplicate supplied entry
                new OrderPartyRoleCommand(payerPartyId, OrderPartyRoleCodes.Payer),       // duplicates the auto Payer
            },
        });

        var roles = await context.OrderPartyRoles.Where(r => r.OrderId == created.Id).ToListAsync();
        roles.Should().HaveCount(2);
        roles.Should().ContainSingle(r => r.PartyId == payerPartyId && r.Role == OrderPartyRoles.Payer);
        roles.Should().ContainSingle(r => r.PartyId == supplierPartyId && r.Role == OrderPartyRoles.Supplier);
    }

    [Fact]
    public async Task CreateAsync_Should_RejectSuppliedPartyRole_WithEmptyPartyOrBlankRole()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        var emptyParty = async () => await service.CreateAsync(ProductPurchaseCommand() with
        {
            PartyRoles = new[] { new OrderPartyRoleCommand(Guid.Empty, OrderPartyRoleCodes.Supplier) },
        });
        await emptyParty.Should().ThrowAsync<ArgumentException>().WithMessage("*PartyId*");

        var blankRole = async () => await service.CreateAsync(ProductPurchaseCommand() with
        {
            PartyRoles = new[] { new OrderPartyRoleCommand(Guid.NewGuid(), "  ") },
        });
        await blankRole.Should().ThrowAsync<ArgumentException>().WithMessage("*Role*");
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

    [Fact]
    public async Task FindByIdempotencyKeyAsync_Should_ReturnTheKeyedOrder_WithTheSameNormalization_AsCreate()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);

        var created = await service.CreateAsync(ProductPurchaseCommand() with { IdempotencyKey = "order-123" });
        await service.CreateAsync(ProductPurchaseCommand() with { IdempotencyKey = "   " }); // blank-key noise, stored NULL

        // Exact key and a whitespace-padded retry key both resolve to the stored (trimmed) order,
        // items included — the lookup a type-specific creation flow uses to settle a lost-response
        // retry BEFORE re-running validation (Spec 053 §12).
        var found = await service.FindByIdempotencyKeyAsync("order-123");
        found.Should().NotBeNull();
        found!.Id.Should().Be(created.Id);
        found.Items.Should().HaveCount(2);
        (await service.FindByIdempotencyKeyAsync("  order-123  "))!.Id.Should().Be(created.Id);

        // An unknown key finds nothing; a blank key NEVER matches (blank keys are stored as NULL —
        // they are non-keys, not a shared bucket).
        (await service.FindByIdempotencyKeyAsync("order-999")).Should().BeNull();
        (await service.FindByIdempotencyKeyAsync("   ")).Should().BeNull();
    }

    [Fact]
    public async Task TransitionAsync_Should_ApplyOnMatchingExpectedFromStatus_AndThrowOnMismatch_LeavingStatusUntouched()
    {
        var (options, tenantId) = NewDb();
        await using var context = CreateDbContext(options, tenantId);
        var service = CreateService(context, tenantId);
        var created = await service.CreateAsync(ProductPurchaseCommand());

        // Compare-and-set (Spec 053 §13): a matching expectation applies as usual…
        var pending = await service.TransitionAsync(
            created.Id, OrderStatuses.Pending, "ready", expectedFromStatus: OrderStatuses.Draft);
        pending.Status.Should().Be(OrderStatuses.Pending);

        // …and a stale expectation (the caller observed Draft, but the order moved on) throws,
        // naming actual vs expected, WITHOUT applying — closing the check-then-act window that
        // would otherwise let a stale caller overwrite an interleaved transition.
        var stale = async () => await service.TransitionAsync(
            created.Id, OrderStatuses.Cancelled, "stale guard", expectedFromStatus: OrderStatuses.Draft);
        await stale.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{OrderStatuses.Pending}*{OrderStatuses.Draft}*");

        (await context.Orders.SingleAsync(o => o.Id == created.Id)).Status.Should().Be(OrderStatuses.Pending);
        // No history row was written for the rejected transition — only the successful one.
        (await context.OrderHistoryEvents.CountAsync(e => e.OrderId == created.Id && e.EventType == "StatusChanged"))
            .Should().Be(1);
    }
}
