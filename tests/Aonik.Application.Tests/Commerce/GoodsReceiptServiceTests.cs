using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Sourcing;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// The Spec 054 receiving flow — the convergence write over the REAL composed services (052
/// InventoryService, 051 IngredientCostService, 052 LowStockAlertService, 053 PurchaseOrderService,
/// 041 CoreOrderService, per the PurchaseOrderServiceTests pattern). Proves the §8 ordering (stock
/// up → cost refresh → alert resolve → PO transition), §9 partial receipts (cumulative
/// received-vs-ordered is DERIVED — the PO stays Pending, never a "PartiallyReceived" status),
/// §10 superseding cost writes, the R4/#164 recovery rule (a short receipt at/below the reorder
/// point leaves the alert exactly as it was), the R7 keyed idempotency (resolve BEFORE any
/// mutation and before the Pending guard, so a retry after completion still resolves), and the §8
/// guards (Pending-only, on-PO-only lines, no over-receipt — each naming the ingredient).
/// </summary>
public class GoodsReceiptServiceTests
{
    private sealed class Harness
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _commerceDb = $"gr_c_{Guid.NewGuid()}";
        private readonly string _orderingDb = $"gr_o_{Guid.NewGuid()}";
        private readonly TestTenantProvider _tenant;
        private readonly TestCurrentUserProvider _user = new();
        private readonly CommerceDbContext _sharedCommerce;

        public Harness()
        {
            _tenant = new TestTenantProvider(_tenantId);
            // ONE CommerceDbContext for the whole composed service graph, mirroring production DI:
            // CommerceModule registers the context and every Commerce service AddScoped, so within
            // a request scope GoodsReceiptService, InventoryService, IngredientCostService, and
            // LowStockAlertService all share the SAME context instance. The Spec 054 resume
            // markers RELY on that — StockAppliedAt/CostAppliedAt are set on the tracked receipt
            // and committed atomically by the next downstream SaveChanges. The clock rides along
            // so CreatedAt (the §8 claim-order tiebreaker) is test-controlled.
            _sharedCommerce = CommerceTestHarness.CreateContext(CommerceOptions(), _tenantId, Clock);
        }

        public CommerceTestHarness.TestClock Clock { get; } = new();

        private DbContextOptions<CommerceDbContext> CommerceOptions() =>
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options;

        /// <summary>A FRESH context over the same store — seeding/asserting outside the service
        /// graph, and standing in for a rival request scope in the concurrency tests.</summary>
        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(CommerceOptions(), _tenantId, Clock);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options, _tenant, _user);

        public CoreOrderService Orders() => new(Ordering(), _tenant, Clock, _user);

        public SupplierService Suppliers() => new(_sharedCommerce, _tenant);

        public PurchaseOrderService PurchaseOrders() => new(_sharedCommerce, Orders(), _tenant);

        public InventoryService Inventory() => new(_sharedCommerce, _tenant, new TenantContext { TenantId = _tenantId }, Clock);

        public LowStockAlertService Alerts() => new(_sharedCommerce, _tenant, new TenantContext { TenantId = _tenantId }, Clock);

        public IngredientCostService Costs() => new(_sharedCommerce, _tenant, Clock);

        public GoodsReceiptService Receipts() => new(_sharedCommerce, Orders(), Inventory(), Costs(), Alerts(), _tenant, Clock);

        public async Task<Guid> SeedIngredientAsync(string name = "Rice", string baseUnit = IngredientBaseUnits.Kg)
        {
            await using var ctx = Commerce();
            var id = Guid.NewGuid();
            ctx.Ingredients.Add(new Ingredient { Id = id, TenantId = _tenantId, Name = name, BaseUnit = baseUnit, IsActive = true });
            await ctx.SaveChangesAsync();
            return id;
        }

        /// <summary>A supplier + one rice catalog row: 25 kg sack @ ₦25,000 → ₦1,000/kg.</summary>
        public async Task<(Guid SupplierId, Guid RiceId)> SeedSupplierWithRiceAsync()
        {
            var rice = await SeedIngredientAsync();
            var supplier = await Suppliers().CreateAsync(new CreateSupplierCommand("Mama Nkechi Farms", "NGN"));
            await Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
                supplier.Id, rice, PackSize: 25m, PackPrice: 25_000m, Sku: "SUP-RICE-25"));
            return (supplier.Id, rice);
        }

        /// <summary>An explicit-lines PO for one ingredient, submitted (Pending — receivable).</summary>
        public async Task<OrderDto> SeedSubmittedPoAsync(Guid supplierId, Guid ingredientId, decimal quantity)
        {
            var po = await PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
                supplierId, new[] { new PurchaseOrderLineCommand(ingredientId, quantity) }));
            return await PurchaseOrders().SubmitAsync(po.Id);
        }
    }

    // ── §8 full receipt: stock ▲, cost ↻, alert ✓, PO → Complete ────────────────────────────────

    [Fact]
    public async Task Receive_Should_ApplyStockCostAndAlert_AndCompleteThePo_OnAFullReceipt()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var rice = StockItemRef.Ingredient(riceId);

        // A standing standard cost (₦900/kg) that the receipt's actual cost must SUPERSEDE, not overwrite.
        await h.Costs().SetCostAsync(new SetIngredientCostCommand(riceId, "NGN", 900m));

        // The 052→053 procurement story: breach → alert → shortfall seed (alert flips Ordered,
        // 2 kg vs reorder point 30 → 2 packs = 50 kg) → submit.
        await h.Inventory().SetOnHandAsync(rice, 2m);
        await h.Inventory().SetReorderPointAsync(rice, 30m);
        (await h.Alerts().ScanAndRaiseAsync()).Raised.Should().Be(1);
        var po = await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));
        await h.PurchaseOrders().SubmitAsync(po.Id);

        // The delivery arrives two days later at an actual ₦1,050/kg.
        h.Clock.UtcNow = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);
        var receipt = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-1", new[] { new ReceiveGoodsLineCommand(riceId, 50m, UnitCostActual: 1_050m) }));

        // The receipt itself.
        receipt.Status.Should().Be(GoodsReceiptStatuses.Posted);
        receipt.PurchaseOrderId.Should().Be(po.Id);
        receipt.ReceivedAt.Should().Be(h.Clock.UtcNow);
        var line = receipt.Lines.Single();
        line.IngredientName.Should().Be("Rice");
        line.QuantityReceived.Should().Be(50m);
        line.OrderedQuantity.Should().Be(50m);
        line.CumulativeReceived.Should().Be(50m);
        line.OnHandAfter.Should().Be(52m);              // 2 on hand + 50 received
        line.Currency.Should().Be("NGN");               // stamped from the PO's CurrencyIn for the cost write

        // STOCK ▲ (052): the default-location level was incremented, not overwritten.
        (await h.Inventory().GetStockLevelAsync(rice)).OnHand.Should().Be(52m);

        // COST ↻ (051, §10): a NEW effective-dated row from ReceivedAt; the prior standard cost is
        // retained with its window closed — history is superseded, never mutated.
        receipt.CostRowsWritten.Should().Be(1);
        (await h.Costs().GetCurrentCostAsync(riceId, "NGN"))!.UnitCost.Should().Be(1_050m);
        var history = await h.Costs().ListHistoryAsync(riceId, "NGN");
        history.Should().HaveCount(2);
        history[0].UnitCost.Should().Be(1_050m);
        history[0].EffectiveFrom.Should().Be(receipt.ReceivedAt);
        history[1].UnitCost.Should().Be(900m);
        history[1].EffectiveTo.Should().Be(receipt.ReceivedAt);

        // ALERT ✓ (§8/R4): available 52 > reorder point 30 — the Ordered alert resolves.
        await using var commerce = h.Commerce();
        var alert = await commerce.LowStockAlerts.SingleAsync();
        alert.Status.Should().Be(LowStockAlertStatuses.Resolved);
        receipt.ResolvedAlertIds.Should().ContainSingle().Which.Should().Be(alert.Id);

        // PO → (041): every line fully received → Complete, with the §8 reason on the spine.
        receipt.PurchaseOrderCompleted.Should().BeTrue();
        receipt.PurchaseOrderStatus.Should().Be(OrderStatusCodes.Complete);
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Complete);
        await using var ordering = h.Ordering();
        var completion = await ordering.OrderHistoryEvents
            .Where(e => e.OrderId == po.Id && e.EventType == "StatusChanged")
            .OrderByDescending(e => e.CreatedAt).FirstAsync();
        completion.DetailsJson.Should().Contain("Fully received");
    }

    // ── §9 partial receipts: cumulative is derived; Pending until the last delivery ─────────────

    [Fact]
    public async Task Receive_Should_KeepThePoPending_AndTrackCumulative_AcrossTwoReceipts()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        // First delivery: 10 of 25 kg. The PO stays OPEN — short is derived, never a status.
        var first = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-p1", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));

        first.PurchaseOrderCompleted.Should().BeFalse();
        first.PurchaseOrderStatus.Should().Be(OrderStatusCodes.Pending);
        first.Lines.Single().CumulativeReceived.Should().Be(10m);
        first.Lines.Single().OrderedQuantity.Should().Be(25m);
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Pending);
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(10m);

        // Second delivery: the remaining 15 kg tops the line up — NOW the PO completes.
        var second = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-p2", new[] { new ReceiveGoodsLineCommand(riceId, 15m) }));

        second.Id.Should().NotBe(first.Id);              // a PO has MANY receipts (§7)
        second.Lines.Single().CumulativeReceived.Should().Be(25m);
        second.PurchaseOrderCompleted.Should().BeTrue();
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Complete);
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(25m);
    }

    // ── R4 / #164: a short receipt must not prematurely resolve the alert ───────────────────────

    [Fact]
    public async Task Receive_Should_LeaveTheAlertExactlyAsItWas_WhenAShortReceiptStaysAtOrBelowTheReorderPoint()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var rice = StockItemRef.Ingredient(riceId);
        await h.Inventory().SetOnHandAsync(rice, 2m);
        await h.Inventory().SetReorderPointAsync(rice, 30m);
        await h.Alerts().ScanAndRaiseAsync();
        var po = await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId)); // 50 kg; alert → Ordered
        await h.PurchaseOrders().SubmitAsync(po.Id);

        (LowStockAlert Snapshot, string Status) before;
        await using (var commerce = h.Commerce())
        {
            var a = await commerce.LowStockAlerts.AsNoTracking().SingleAsync();
            before = (a, a.Status);
        }

        // Only 10 of 50 kg arrives: available 12 is still <= the 30 kg reorder point — the
        // shortage is NOT fixed, so the Ordered alert stays exactly as it was (no resolve, no
        // snapshot churn), and the PO stays Pending.
        var shortReceipt = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-s1", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));

        shortReceipt.ResolvedAlertIds.Should().BeEmpty();
        shortReceipt.PurchaseOrderCompleted.Should().BeFalse();
        await using (var commerce = h.Commerce())
        {
            var after = await commerce.LowStockAlerts.AsNoTracking().SingleAsync();
            after.Status.Should().Be(LowStockAlertStatuses.Ordered);
            after.Status.Should().Be(before.Status);
            after.AvailableAtRaise.Should().Be(before.Snapshot.AvailableAtRaise);
            after.ReorderPoint.Should().Be(before.Snapshot.ReorderPoint);
            after.RaisedAt.Should().Be(before.Snapshot.RaisedAt);
        }

        // The remaining 40 kg lands: available 52 clears the threshold — NOW the alert resolves
        // and the PO completes.
        var completing = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-s2", new[] { new ReceiveGoodsLineCommand(riceId, 40m) }));

        completing.ResolvedAlertIds.Should().HaveCount(1);
        completing.PurchaseOrderCompleted.Should().BeTrue();
        await using var commerce2 = h.Commerce();
        (await commerce2.LowStockAlerts.SingleAsync()).Status.Should().Be(LowStockAlertStatuses.Resolved);
    }

    // ── R7 idempotency: resolve by key BEFORE any mutation (and before the Pending guard) ───────

    [Fact]
    public async Task Receive_Should_ReturnTheExistingReceipt_AndApplyNothingTwice_OnAKeyedRetry()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        var command = new ReceiveGoodsCommand(
            po.Id, "grn-dup", new[] { new ReceiveGoodsLineCommand(riceId, 25m, UnitCostActual: 1_050m) });
        var first = await h.Receipts().ReceiveAsync(command);
        first.PurchaseOrderCompleted.Should().BeTrue();

        // A lost-response retry re-sends the SAME command. The PO is now Complete — the Pending
        // guard would reject it — but the key resolves FIRST (§8, the Spec 053 lesson), returning
        // the existing receipt with NOTHING re-applied.
        var retry = await h.Receipts().ReceiveAsync(command);

        retry.Id.Should().Be(first.Id);
        retry.PurchaseOrderCompleted.Should().BeTrue();
        retry.Lines.Single().CumulativeReceived.Should().Be(25m);
        retry.ResolvedAlertIds.Should().BeEmpty();       // call-scoped: this call applied nothing
        retry.CostRowsWritten.Should().Be(0);

        // Stock incremented ONCE, one receipt row, one cost row — never double-counted.
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(25m);
        (await h.Costs().ListHistoryAsync(riceId, "NGN")).Should().HaveCount(1);
        await using var commerce = h.Commerce();
        (await commerce.GoodsReceipts.CountAsync()).Should().Be(1);
        (await commerce.GoodsReceiptLines.CountAsync()).Should().Be(1);
    }

    // ── §8 guards ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_Should_Reject_ADraftPo_AndANonPurchaseOrder()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();

        // Draft = never placed with the supplier — nothing can have physically arrived.
        var draft = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }));
        var onDraft = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            draft.Id, "grn-d", new[] { new ReceiveGoodsLineCommand(riceId, 25m) }));
        await onDraft.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Draft*only a submitted (Pending)*");

        // A retail order on the same spine is not receivable.
        var retail = await h.Orders().CreateAsync(new CreateOrderCommand(
            OrderTypeCodes.ProductPurchase, Guid.NewGuid(), "NGN",
            new[] { new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 5_000m, "NGN") }));
        var onRetail = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            retail.Id, "grn-r", new[] { new ReceiveGoodsLineCommand(riceId, 25m) }));
        await onRetail.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a purchase order*");

        // Neither rejection moved any stock.
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(0m);
    }

    [Fact]
    public async Task Receive_Should_Reject_AnIngredientNotOnThePurchaseOrder_NamingIt()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var beans = await h.SeedIngredientAsync("Beans");
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        var act = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-stray", new[]
            {
                new ReceiveGoodsLineCommand(riceId, 10m),
                new ReceiveGoodsLineCommand(beans, 5m), // never ordered on this PO
            }));

        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Contain("'Beans'").And.NotContain("'Rice'");

        // All-or-nothing: the valid rice line was not applied either.
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(0m);
    }

    [Fact]
    public async Task Receive_Should_Reject_OverReceipt_CumulativelyAcrossReceipts_NamingTheIngredient()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        // A single receipt over the ordered quantity (v1 tolerance = none).
        var oneShot = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-o1", new[] { new ReceiveGoodsLineCommand(riceId, 30m) }));
        (await oneShot.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("'Rice'").And.Contain("ordered 25").And.Contain("this receipt 30");

        // CUMULATIVE over-receipt: 20 already received, so a further 10 would exceed 25.
        await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-o2", new[] { new ReceiveGoodsLineCommand(riceId, 20m) }));
        var cumulative = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-o3", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));
        (await cumulative.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("'Rice'").And.Contain("already received 20");

        // Only the accepted 20 kg receipt moved stock; the PO is still open for the last 5 kg.
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(20m);
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Pending);
    }

    [Fact]
    public async Task Receive_Should_Reject_MalformedCommands_BeforeTouchingAnything()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        // Non-positive quantity (validated on the normalized 4 dp value).
        var zeroQty = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-g1", new[] { new ReceiveGoodsLineCommand(riceId, 0m) }));
        await zeroQty.Should().ThrowAsync<ArgumentException>().WithMessage("*must be positive*");
        var negativeQty = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-g2", new[] { new ReceiveGoodsLineCommand(riceId, -5m) }));
        await negativeQty.Should().ThrowAsync<ArgumentException>().WithMessage("*must be positive*");

        // Non-positive actual cost.
        var zeroCost = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-g3", new[] { new ReceiveGoodsLineCommand(riceId, 5m, UnitCostActual: 0m) }));
        await zeroCost.Should().ThrowAsync<ArgumentException>().WithMessage("*cost must be positive*");

        // The idempotency key is REQUIRED — it is the whole §8 retry story.
        var noKey = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "  ", new[] { new ReceiveGoodsLineCommand(riceId, 5m) }));
        await noKey.Should().ThrowAsync<ArgumentException>().WithMessage("*idempotency key*");

        // One line per ingredient per receipt (duplicates would blur cost + cumulative semantics).
        var dupLines = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-g4", new[]
            {
                new ReceiveGoodsLineCommand(riceId, 5m),
                new ReceiveGoodsLineCommand(riceId, 5m),
            }));
        await dupLines.Should().ThrowAsync<ArgumentException>().WithMessage("*one line per ingredient*");

        // NULL and EMPTY lines are the same domain error — never an NRE. (The endpoint projects a
        // null request body list to empty and lets this validation speak.)
        var nullLines = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-g5", null!));
        await nullLines.Should().ThrowAsync<ArgumentException>().WithMessage("*at least one line*");
        var emptyLines = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-g6", Array.Empty<ReceiveGoodsLineCommand>()));
        await emptyLines.Should().ThrowAsync<ArgumentException>().WithMessage("*at least one line*");

        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(0m);
    }

    // ── §10: a cost-less line refreshes stock only ──────────────────────────────────────────────

    [Fact]
    public async Task Receive_Should_NotWriteAnIngredientCost_WhenNoLineCarriesAnActualCost()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        await h.Costs().SetCostAsync(new SetIngredientCostCommand(riceId, "NGN", 900m)); // standing standard cost
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        var receipt = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-nocost", new[] { new ReceiveGoodsLineCommand(riceId, 25m) }));

        // Stock moved; the ingredient keeps its existing cost — no new row, no currency stamp.
        receipt.CostRowsWritten.Should().Be(0);
        receipt.Lines.Single().UnitCostActual.Should().BeNull();
        receipt.Lines.Single().Currency.Should().BeNull();
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(25m);
        (await h.Costs().GetCurrentCostAsync(riceId, "NGN"))!.UnitCost.Should().Be(900m);
        (await h.Costs().ListHistoryAsync(riceId, "NGN")).Should().HaveCount(1);
    }

    // ── §8 over-receipt RACE: post-claim re-validation, deterministic winner, void-self ──────────

    [Fact]
    public async Task Receive_Should_VoidItself_AndSurfaceTheConflict_WhenARivalClaimWinsTheRaceWindow()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        // The §8 race: a rival receive with a DIFFERENT key claims 20 kg between our pre-claim
        // cumulative snapshot (which saw 0 received) and our claim commit — exactly the window the
        // pre-claim check cannot see. The seam commits the rival claim directly (a rival request
        // scope = a fresh context) with an EARLIER CreatedAt, then advances the clock so our claim
        // stamps later: the rival deterministically wins the (CreatedAt, Id) order.
        var rivalLines = new[] { new ReceiveGoodsLineCommand(riceId, 20m) };
        var receipts = h.Receipts();
        receipts.OnBeforeClaimForTests = async _ =>
        {
            await using var rivalScope = h.Commerce();
            var rivalId = Guid.NewGuid();
            rivalScope.GoodsReceipts.Add(new GoodsReceipt
            {
                Id = rivalId,
                PurchaseOrderId = po.Id,
                IdempotencyKey = "grn-race-rival",
                PayloadHash = GoodsReceiptService.ComputePayloadHash(po.Id, rivalLines),
                ReceivedAt = h.Clock.UtcNow,
                Status = GoodsReceiptStatuses.Posted,
            });
            rivalScope.GoodsReceiptLines.Add(new GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                GoodsReceiptId = rivalId,
                IngredientId = riceId,
                QuantityReceived = 20m,
            });
            await rivalScope.SaveChangesAsync();          // rival CreatedAt = now
            h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(5); // our claim stamps LATER — rival wins
        };

        var command = new ReceiveGoodsCommand(po.Id, "grn-race", new[] { new ReceiveGoodsLineCommand(riceId, 10m) });
        var act = async () => await receipts.ReceiveAsync(command);

        // The later claim re-sums committed state, sees 20 (rival) + 10 (self) > 25, VOIDS ITSELF,
        // and conflicts naming the ingredient with the ordered / earlier-received / this figures.
        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Contain("'Rice'")
            .And.Contain("ordered 25").And.Contain("earlier receipts 20").And.Contain("this receipt 10")
            .And.Contain("VOIDED");

        // Audit-preserving void: the losing receipt row exists, Voided, and applied NOTHING.
        await using (var commerce = h.Commerce())
        {
            var voided = await commerce.GoodsReceipts.SingleAsync(r => r.IdempotencyKey == "grn-race");
            voided.Status.Should().Be(GoodsReceiptStatuses.Voided);
            voided.StockAppliedAt.Should().BeNull();
            voided.CostAppliedAt.Should().BeNull();
        }
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(0m);

        // A keyed retry of the VOIDED receipt surfaces the conflict — never success.
        var retryVoided = async () => await h.Receipts().ReceiveAsync(command);
        (await retryVoided.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("VOIDED").And.Contain("NEW idempotency key");

        // Voided receipts are EXCLUDED from every cumulative sum: the remaining 5 kg (25 ordered −
        // 20 rival) is still receivable under a new key, and completes the PO.
        var remainder = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-race-rest", new[] { new ReceiveGoodsLineCommand(riceId, 5m) }));
        remainder.Lines.Single().CumulativeReceived.Should().Be(25m); // 20 + 5; the voided 10 counts nowhere
        remainder.PurchaseOrderCompleted.Should().BeTrue();
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Complete);
    }

    // ── §8/§9 completion recomputed POST-claim from ALL committed receipts (concurrent siblings) ─

    [Fact]
    public async Task Receive_Should_CompleteThePo_WhenAConcurrentSiblingReceiptCoversTheComplementaryLines()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var beansId = await h.SeedIngredientAsync("Beans");
        var po = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[]
            {
                new PurchaseOrderLineCommand(riceId, 10m),
                new PurchaseOrderLineCommand(beansId, 5m, UnitPrice: 500m), // no catalog row — explicit price
            }));
        await h.PurchaseOrders().SubmitAsync(po.Id);

        // OUR receive covers ONLY the beans. In its race window a full sibling receive lands the
        // rice — after our pre-claim snapshot (which saw nothing received), so only the POST-claim
        // completion recompute over committed rows can know the PO is now fully covered.
        GoodsReceiptDto? sibling = null;
        var receipts = h.Receipts();
        receipts.OnBeforeClaimForTests = async _ =>
        {
            sibling = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
                po.Id, "grn-sib-rice", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));
            h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(5);
        };

        var beansReceipt = await receipts.ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-sib-beans", new[] { new ReceiveGoodsLineCommand(beansId, 5m) }));

        // The sibling saw beans still owed → left the PO Pending; our post-claim recompute sums
        // BOTH committed receipts → rice 10/10 + beans 5/5 → Complete.
        sibling!.PurchaseOrderCompleted.Should().BeFalse();
        beansReceipt.PurchaseOrderCompleted.Should().BeTrue();
        beansReceipt.PurchaseOrderStatus.Should().Be(OrderStatusCodes.Complete);
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Complete);
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(10m);
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(beansId))).OnHand.Should().Be(5m);
    }

    [Fact]
    public async Task TransitionPoToComplete_Should_TreatAnAlreadyCompletePo_AsSuccess_AndRethrowOtherMismatches()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var receipts = h.Receipts();

        // A sibling receipt won the Pending→Complete compare-and-set between our recompute and our
        // transition: the expected-from mismatch lands on an ALREADY Complete order — same outcome,
        // treated as success (the sequential flow cannot produce this interleaving, so the internal
        // helper is driven directly, per the FlipSourceAlertsToOrderedAsync precedent).
        var completedPo = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);
        await h.Orders().TransitionAsync(completedPo.Id, OrderStatusCodes.Complete, "sibling receipt won");
        var after = await receipts.TransitionPoToCompleteAsync(completedPo.Id, CancellationToken.None);
        after.Status.Should().Be(OrderStatusCodes.Complete);

        // ANY OTHER mismatch stays loud: an operator cancel racing the receipt must not be
        // swallowed (and a terminal PO must never be resurrected).
        var cancelledPo = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);
        await h.Orders().TransitionAsync(cancelledPo.Id, OrderStatusCodes.Cancelled, "operator cancel");
        var act = async () => await receipts.TransitionPoToCompleteAsync(cancelledPo.Id, CancellationToken.None);
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("Cancelled").And.Contain("not the expected Pending");
    }

    // ── §10 pre-claim cost validation: an un-writable cost rejects the WHOLE receive up-front ────

    [Fact]
    public async Task Receive_Should_RejectTheWholeReceive_BeforeClaimingAnything_WhenABackdatedCostLandsInAnElapsedWindow()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();

        // Cost history: [Jun 1 → Jun 10) at ₦900 (fully elapsed), [Jun 10 → open) at ₦1,000.
        h.Clock.UtcNow = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        await h.Costs().SetCostAsync(new SetIngredientCostCommand(riceId, "NGN", 900m));
        h.Clock.UtcNow = new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc);
        await h.Costs().SetCostAsync(new SetIngredientCostCommand(riceId, "NGN", 1_000m));
        h.Clock.UtcNow = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);

        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        // A cost-carrying receipt backdated INTO the elapsed window: Spec 051's history-rewrite
        // guard refuses that cost write — validated BEFORE the claim, so the whole receive is
        // rejected with NO receipt row and NO stock movement (previously the refusal surfaced only
        // AFTER stock had already been incremented).
        var act = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-backdated",
            new[] { new ReceiveGoodsLineCommand(riceId, 25m, UnitCostActual: 1_100m) },
            ReceivedAt: new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc)));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("already priced the past");

        await using var commerce = h.Commerce();
        (await commerce.GoodsReceipts.CountAsync()).Should().Be(0);   // nothing claimed
        (await commerce.IngredientCosts.CountAsync()).Should().Be(2); // history untouched
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(0m);
    }

    // ── §8/R7 resume: a keyed retry applies exactly the steps the crash left unapplied ───────────

    [Fact]
    public async Task Receive_Should_ResumeAClaimedButUnappliedReceipt_ApplyingStockAndCostOnce_OnAKeyedRetry()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var rice = StockItemRef.Ingredient(riceId);
        await h.Inventory().SetOnHandAsync(rice, 2m);
        await h.Inventory().SetReorderPointAsync(rice, 30m);
        (await h.Alerts().ScanAndRaiseAsync()).Raised.Should().Be(1);
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 50m);

        // The INTERMEDIATE crash state (§8): the claim committed — receipt + lines, applied
        // markers null — and the process died before any stock/cost moved. Seeded directly, with
        // the exact payload hash the retry recomputes.
        var lines = new[] { new ReceiveGoodsLineCommand(riceId, 50m, UnitCostActual: 1_050m) };
        var receiptId = Guid.NewGuid();
        await using (var crashScope = h.Commerce())
        {
            crashScope.GoodsReceipts.Add(new GoodsReceipt
            {
                Id = receiptId,
                PurchaseOrderId = po.Id,
                IdempotencyKey = "grn-resume",
                PayloadHash = GoodsReceiptService.ComputePayloadHash(po.Id, lines),
                ReceivedAt = h.Clock.UtcNow,
                Status = GoodsReceiptStatuses.Posted,
            });
            crashScope.GoodsReceiptLines.Add(new GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                GoodsReceiptId = receiptId,
                IngredientId = riceId,
                QuantityReceived = 50m,
                UnitCostActual = 1_050m,
                Currency = "NGN",
            });
            await crashScope.SaveChangesAsync();
        }

        // The keyed retry RESUMES: stock and cost apply exactly once, the markers flip, and the
        // always-idempotent tail resolves the recovered alert and completes the PO — the same
        // response a fresh receive would have returned.
        var resumed = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(po.Id, "grn-resume", lines));

        resumed.Id.Should().Be(receiptId);
        resumed.Status.Should().Be(GoodsReceiptStatuses.Posted);
        resumed.CostRowsWritten.Should().Be(1);                     // THIS call applied the cost
        resumed.ResolvedAlertIds.Should().HaveCount(1);             // and the tail resolved the alert
        resumed.PurchaseOrderCompleted.Should().BeTrue();
        resumed.Lines.Single().CumulativeReceived.Should().Be(50m);
        resumed.Lines.Single().OnHandAfter.Should().Be(52m);

        (await h.Inventory().GetStockLevelAsync(rice)).OnHand.Should().Be(52m); // 2 + 50, ONCE
        (await h.Costs().GetCurrentCostAsync(riceId, "NGN"))!.UnitCost.Should().Be(1_050m);
        (await h.Orders().GetAsync(po.Id))!.Status.Should().Be(OrderStatusCodes.Complete);
        await using (var commerce = h.Commerce())
        {
            var receipt = await commerce.GoodsReceipts.SingleAsync();
            receipt.StockAppliedAt.Should().NotBeNull();
            receipt.CostAppliedAt.Should().NotBeNull();
            (await commerce.LowStockAlerts.SingleAsync()).Status.Should().Be(LowStockAlertStatuses.Resolved);
        }

        // A FURTHER retry finds both markers set: nothing re-applies, same receipt, same live view.
        var again = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(po.Id, "grn-resume", lines));
        again.Id.Should().Be(receiptId);
        again.CostRowsWritten.Should().Be(0);
        again.ResolvedAlertIds.Should().BeEmpty();
        again.PurchaseOrderCompleted.Should().BeTrue();
        (await h.Inventory().GetStockLevelAsync(rice)).OnHand.Should().Be(52m);
        (await h.Costs().ListHistoryAsync(riceId, "NGN")).Should().HaveCount(1);
    }

    // ── §8 payload pinning: a reused key must describe the SAME logical receive ──────────────────

    [Fact]
    public async Task Receive_Should_Conflict_WhenTheKeyIsReusedWithADifferentPayload_AndResume_WhenIdentical()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var po = await h.SeedSubmittedPoAsync(supplierId, riceId, quantity: 25m);

        var original = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-pin", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));

        // Same key, different quantity → conflict, NOT a silent no-op returning the 10 kg receipt.
        var differentLines = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-pin", new[] { new ReceiveGoodsLineCommand(riceId, 12m) }));
        (await differentLines.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("DIFFERENT payload");

        // Same key, different PO → conflict naming the stored PO (checked before the PO is even
        // loaded — the key alone proves the reuse).
        var differentPo = async () => await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            Guid.NewGuid(), "grn-pin", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));
        (await differentPo.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain($"'{po.Id}'").And.Contain("cannot be reused");

        // Same key, SAME payload → the resume path: same receipt, nothing re-applied.
        var retry = await h.Receipts().ReceiveAsync(new ReceiveGoodsCommand(
            po.Id, "grn-pin", new[] { new ReceiveGoodsLineCommand(riceId, 10m) }));
        retry.Id.Should().Be(original.Id);
        retry.CostRowsWritten.Should().Be(0);
        (await h.Inventory().GetStockLevelAsync(StockItemRef.Ingredient(riceId))).OnHand.Should().Be(10m);
        await using var commerce = h.Commerce();
        (await commerce.GoodsReceipts.CountAsync()).Should().Be(1);
    }

    // ── 052/054 AdjustOnHandAsync: bounded reload-and-reapply on rowversion conflicts ────────────
    // InMemory never raises rowversion conflicts on its own (it neither bumps nor enforces the
    // token), so the conflict is INJECTED via a SaveChanges interceptor while a rival commit lands
    // through a separate context — the exact shape SQL Server produces when the RowVersion token
    // catches a lost update.

    private sealed class ConcurrencyConflictInterceptor : SaveChangesInterceptor
    {
        private int _remainingConflicts;
        private Func<Task>? _rivalWrite;

        public int SaveAttempts { get; private set; }

        public void Arm(int conflicts, Func<Task>? rivalWrite = null)
        {
            _remainingConflicts = conflicts;
            _rivalWrite = rivalWrite;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (_remainingConflicts > 0)
            {
                _remainingConflicts--;
                if (_rivalWrite is not null)
                {
                    await _rivalWrite(); // the rival's commit that SQL Server would have detected
                }
                throw new DbUpdateConcurrencyException("Simulated rowversion conflict.");
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static (DbContextOptions<CommerceDbContext> Plain, DbContextOptions<CommerceDbContext> Intercepted)
        ConflictDbOptions(ConcurrencyConflictInterceptor interceptor)
    {
        var dbName = $"gr_adj_{Guid.NewGuid()}";
        return (
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(dbName).Options,
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(dbName).AddInterceptors(interceptor).Options);
    }

    [Fact]
    public async Task AdjustOnHand_Should_ReloadAndReapplyTheSignedDelta_WhenTheSaveHitsARowversionConflict()
    {
        var interceptor = new ConcurrencyConflictInterceptor();
        var (plain, intercepted) = ConflictDbOptions(interceptor);
        var tenantId = Guid.NewGuid();
        var riceId = Guid.NewGuid();
        await using (var seed = CommerceTestHarness.CreateContext(plain, tenantId))
        {
            seed.Ingredients.Add(new Ingredient { Id = riceId, TenantId = tenantId, Name = "Rice", BaseUnit = IngredientBaseUnits.Kg, IsActive = true });
            seed.InventoryLevels.Add(new InventoryLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IngredientId = riceId,
                StockItemKind = StockItemKinds.Ingredient,
                OnHand = 10m,
            });
            await seed.SaveChangesAsync();
        }

        await using var ctx = CommerceTestHarness.CreateContext(intercepted, tenantId);
        var inventory = new InventoryService(ctx, new TestTenantProvider(tenantId), new TenantContext { TenantId = tenantId }, new CommerceTestHarness.TestClock());

        // First save conflicts AFTER a rival's +5 commits; the retry must reload the level fresh
        // and re-apply OUR signed delta on top — increments commute, so both movements survive.
        interceptor.Arm(conflicts: 1, rivalWrite: async () =>
        {
            await using var rival = CommerceTestHarness.CreateContext(plain, tenantId);
            var level = await rival.InventoryLevels.SingleAsync(l => l.IngredientId == riceId);
            level.OnHand += 5m;
            await rival.SaveChangesAsync();
        });

        var result = await inventory.AdjustOnHandAsync(StockItemRef.Ingredient(riceId), 7m);

        result.OnHand.Should().Be(22m); // 10 seeded + 5 rival + 7 ours — nothing lost, nothing doubled
        interceptor.SaveAttempts.Should().Be(2);
        await using var assert = CommerceTestHarness.CreateContext(plain, tenantId);
        (await assert.InventoryLevels.SingleAsync(l => l.IngredientId == riceId)).OnHand.Should().Be(22m);
    }

    [Fact]
    public async Task AdjustOnHand_Should_RethrowAfterExhaustingTheBoundedRetries_WhenTheConflictPersists()
    {
        var interceptor = new ConcurrencyConflictInterceptor();
        var (plain, intercepted) = ConflictDbOptions(interceptor);
        var tenantId = Guid.NewGuid();
        var riceId = Guid.NewGuid();
        await using (var seed = CommerceTestHarness.CreateContext(plain, tenantId))
        {
            seed.Ingredients.Add(new Ingredient { Id = riceId, TenantId = tenantId, Name = "Rice", BaseUnit = IngredientBaseUnits.Kg, IsActive = true });
            seed.InventoryLevels.Add(new InventoryLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IngredientId = riceId,
                StockItemKind = StockItemKinds.Ingredient,
                OnHand = 10m,
            });
            await seed.SaveChangesAsync();
        }

        await using var ctx = CommerceTestHarness.CreateContext(intercepted, tenantId);
        var inventory = new InventoryService(ctx, new TestTenantProvider(tenantId), new TenantContext { TenantId = tenantId }, new CommerceTestHarness.TestClock());

        // Pathological contention: every attempt conflicts. The loop is BOUNDED — after the third
        // attempt the exception propagates (fails loudly, never spins or silently drops the delta).
        interceptor.Arm(conflicts: int.MaxValue);
        var act = async () => await inventory.AdjustOnHandAsync(StockItemRef.Ingredient(riceId), 7m);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        interceptor.SaveAttempts.Should().Be(3);
        await using var assert = CommerceTestHarness.CreateContext(plain, tenantId);
        (await assert.InventoryLevels.SingleAsync(l => l.IngredientId == riceId)).OnHand.Should().Be(10m); // untouched
    }
}
