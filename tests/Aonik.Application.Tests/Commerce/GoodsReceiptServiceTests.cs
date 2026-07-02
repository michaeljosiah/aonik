using Aonik.Commerce.Contracts.Models.Sourcing;
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

        public Harness() => _tenant = new TestTenantProvider(_tenantId);

        public CommerceTestHarness.TestClock Clock { get; } = new();

        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options, _tenantId);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options, _tenant, _user);

        public CoreOrderService Orders() => new(Ordering(), _tenant, Clock, _user);

        public SupplierService Suppliers() => new(Commerce(), _tenant);

        public PurchaseOrderService PurchaseOrders() => new(Commerce(), Orders(), _tenant);

        public InventoryService Inventory() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, Clock);

        public LowStockAlertService Alerts() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, Clock);

        public IngredientCostService Costs() => new(Commerce(), _tenant, Clock);

        public GoodsReceiptService Receipts() => new(Commerce(), Orders(), Inventory(), Costs(), Alerts(), _tenant, Clock);

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
}
