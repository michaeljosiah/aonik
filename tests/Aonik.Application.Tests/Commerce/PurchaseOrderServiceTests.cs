using System.Text.Json;

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
/// Purchase orders on the shared Order spine (Spec 053 §10–§13). The PO is NOT a Commerce entity:
/// these tests prove the §10 OrderItem line mapping (ProductId = IngredientId soft-ref, DetailsJson
/// discriminator), the §11 direction of money (PayerPartyId null; Supplier role only when
/// party-linked; supplier identity always in ProvenanceJson), §10 price resolution (explicit wins;
/// else catalog PackPrice/PackSize; neither → rejected naming the ingredient), the §12 shortfall
/// seed (pack rounding, ReorderQuantity override, alerts flipped to Ordered), and the §13
/// lifecycle guards this service enforces over the spine's guard-free TransitionAsync — all on the
/// existing OrderStatusCodes. Uses the real CoreOrderService, per the CheckoutServiceTests pattern.
/// </summary>
public class PurchaseOrderServiceTests
{
    private sealed class Harness
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _commerceDb = $"po_c_{Guid.NewGuid()}";
        private readonly string _orderingDb = $"po_o_{Guid.NewGuid()}";
        private readonly TestTenantProvider _tenant;
        private readonly TestCurrentUserProvider _user = new();
        private readonly CommerceTestHarness.TestClock _clock = new();

        public Harness() => _tenant = new TestTenantProvider(_tenantId);

        public Guid TenantId => _tenantId;

        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options, _tenantId);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options, _tenant, _user);

        public CoreOrderService Orders() => new(Ordering(), _tenant, _clock, _user);

        public SupplierService Suppliers() => new(Commerce(), _tenant);

        public PurchaseOrderService PurchaseOrders() => new(Commerce(), Orders(), _tenant);

        public InventoryService Inventory() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, _clock);

        public LowStockAlertService Alerts() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, _clock);

        public async Task<Guid> SeedIngredientAsync(string name = "Rice", string baseUnit = IngredientBaseUnits.Kg, bool isActive = true)
        {
            await using var ctx = Commerce();
            var id = Guid.NewGuid();
            ctx.Ingredients.Add(new Ingredient { Id = id, TenantId = _tenantId, Name = name, BaseUnit = baseUnit, IsActive = isActive });
            await ctx.SaveChangesAsync();
            return id;
        }

        /// <summary>A supplier + one rice catalog row: 25 kg sack @ ₦25,000 → ₦1,000/kg.</summary>
        public async Task<(Guid SupplierId, Guid RiceId)> SeedSupplierWithRiceAsync(Guid? partyId = null)
        {
            var rice = await SeedIngredientAsync();
            var supplier = await Suppliers().CreateAsync(new CreateSupplierCommand("Mama Nkechi Farms", "NGN", partyId));
            await Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
                supplier.Id, rice, PackSize: 25m, PackPrice: 25_000m, Sku: "SUP-RICE-25"));
            return (supplier.Id, rice);
        }
    }

    // ── §10 create + line mapping, §11 direction of money ──────────────────────────────────────

    [Fact]
    public async Task Create_Should_CreateSpineOrder_WithPurchaseOrderLineMapping_AndSupplierProvenance()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();

        var order = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, Quantity: 100m) }, Notes: "for the weekend rush"));

        // The PO is a real Order on the spine — the existing status codes, no new entity.
        order.OrderType.Should().Be(OrderTypeCodes.PurchaseOrder);
        order.Status.Should().Be(OrderStatusCodes.Draft);
        order.CurrencyIn.Should().Be("NGN");           // defaulted from the supplier
        order.PayerPartyId.Should().BeNull();          // the tenant is the payer — no Party row (§11)
        order.AmountIn.Should().Be(100_000m);          // 100 kg × ₦1,000/kg

        // §10 column mapping on the single line.
        var line = order.Items.Single();
        line.ItemType.Should().Be(OrderTypeCodes.PurchaseOrder);
        line.ItemIndex.Should().Be(0);
        line.Quantity.Should().Be(100m);
        line.UnitPrice.Should().Be(1_000m);            // PackPrice / PackSize
        line.AmountIn.Should().Be(100_000m);           // Quantity × UnitPrice
        line.ProductId.Should().Be(riceId);            // the documented soft-ref reinterpretation
        line.Sku.Should().Be("SUP-RICE-25");           // the supplier's SKU from the catalog row

        // The DetailsJson discriminator makes the reuse self-describing.
        using var details = JsonDocument.Parse(line.DetailsJson);
        details.RootElement.GetProperty("kind").GetString().Should().Be("purchase-order-line");
        details.RootElement.GetProperty("ingredientId").GetGuid().Should().Be(riceId);
        details.RootElement.GetProperty("unit").GetString().Should().Be(IngredientBaseUnits.Kg);

        // Supplier identity ALWAYS travels in ProvenanceJson (§11) — self-describing even unlinked.
        await using var ordering = h.Ordering();
        var persisted = await ordering.Orders.SingleAsync(o => o.Id == order.Id);
        using var provenance = JsonDocument.Parse(persisted.ProvenanceJson);
        provenance.RootElement.GetProperty("kind").GetString().Should().Be("purchase-order");
        provenance.RootElement.GetProperty("supplierId").GetGuid().Should().Be(supplierId);
        provenance.RootElement.GetProperty("supplierName").GetString().Should().Be("Mama Nkechi Farms");
        provenance.RootElement.GetProperty("notes").GetString().Should().Be("for the weekend rush");

        // Unlinked supplier + null payer → NO party-role rows at all; the direction of money is
        // carried by the order type + provenance, never by a fabricated tenant party (§11).
        (await ordering.OrderPartyRoles.CountAsync(r => r.OrderId == order.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Create_Should_WriteSupplierPartyRole_OnlyWhenSupplierIsPartyLinked()
    {
        var h = new Harness();
        var supplierPartyId = Guid.NewGuid();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync(partyId: supplierPartyId);

        var order = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 50m) }));

        await using var ordering = h.Ordering();
        var roles = await ordering.OrderPartyRoles.Where(r => r.OrderId == order.Id).ToListAsync();
        roles.Should().ContainSingle();
        roles[0].Role.Should().Be(OrderPartyRoleCodes.Supplier);
        roles[0].PartyId.Should().Be(supplierPartyId);
    }

    // ── §10 price resolution ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Should_PreferExplicitUnitPrice_OverCatalogDerivedPrice()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync(); // catalog says ₦1,000/kg

        var order = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 100m, UnitPrice: 900m) }));

        order.Items.Single().UnitPrice.Should().Be(900m);
        order.AmountIn.Should().Be(90_000m);
    }

    [Fact]
    public async Task Create_Should_Reject_NamingEveryIngredient_WithNoResolvablePrice()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var beans = await h.SeedIngredientAsync("Beans");   // no catalog row
        var garri = await h.SeedIngredientAsync("Garri");   // no catalog row

        var act = async () => await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[]
            {
                new PurchaseOrderLineCommand(riceId, 25m),                    // priceable from the catalog
                new PurchaseOrderLineCommand(beans, 10m),                     // neither explicit nor catalog
                new PurchaseOrderLineCommand(garri, 5m, UnitPrice: 800m),     // explicit price — fine
            }));

        // The error names exactly the unpriceable ingredient(s).
        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Contain("'Beans'").And.NotContain("'Rice'").And.NotContain("'Garri'");
    }

    [Fact]
    public async Task Create_Should_Reject_WhenCatalogCurrencyDiffersFromOrderCurrency()
    {
        var h = new Harness();
        var rice = await h.SeedIngredientAsync();
        var supplier = await h.Suppliers().CreateAsync(new CreateSupplierCommand("Import Co", "NGN"));
        await h.Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplier.Id, rice, 25m, 200m, Currency: "GBP"));

        // Deriving a GBP pack price into an NGN order would silently mis-price the line.
        var act = async () => await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplier.Id, new[] { new PurchaseOrderLineCommand(rice, 25m) }));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*GBP*");
    }

    [Fact]
    public async Task Create_Should_Reject_InactiveSupplier_And_InactiveIngredient()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();

        var stale = await h.SeedIngredientAsync("Old Spice Mix", isActive: false);
        var inactiveIngredient = async () => await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(stale, 5m, UnitPrice: 100m) }));
        await inactiveIngredient.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'Old Spice Mix'*");

        await h.Suppliers().UpdateAsync(new UpdateSupplierCommand(supplierId, "Mama Nkechi Farms", "NGN", IsActive: false));
        var inactiveSupplier = async () => await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }));
        await inactiveSupplier.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactive*");
    }

    [Fact]
    public async Task Create_Should_PassIdempotencyKeyThroughToTheSpine()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var command = new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }, IdempotencyKey: "po-seed-1");

        var first = await h.PurchaseOrders().CreateAsync(command);
        var retry = await h.PurchaseOrders().CreateAsync(command);

        retry.Id.Should().Be(first.Id);
        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(1);
    }

    // ── §12 shortfall seed ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFromShortfall_Should_PackRoundTheShortfall_PriceFromCatalog_AndFlipAlertsOrdered()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync(); // 25 kg sack @ ₦25,000

        // Available 2 kg vs reorder point 30 kg → shortfall 28 kg → ceil(28/25) = 2 sacks = 50 kg.
        var rice = StockItemRef.Ingredient(riceId);
        await h.Inventory().SetOnHandAsync(rice, 2m);
        await h.Inventory().SetReorderPointAsync(rice, 30m);
        (await h.Alerts().ScanAndRaiseAsync()).Raised.Should().Be(1);

        var order = await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));

        order.OrderType.Should().Be(OrderTypeCodes.PurchaseOrder);
        order.Status.Should().Be(OrderStatusCodes.Draft);
        var line = order.Items.Single();
        line.ProductId.Should().Be(riceId);
        line.Quantity.Should().Be(50m);                // whole packs, in base units
        line.UnitPrice.Should().Be(1_000m);            // PackPrice / PackSize
        line.AmountIn.Should().Be(50_000m);
        line.Sku.Should().Be("SUP-RICE-25");

        // The source alert flipped to Ordered in the same operation, and is referenced in the provenance.
        await using var commerce = h.Commerce();
        var alert = await commerce.LowStockAlerts.SingleAsync();
        alert.Status.Should().Be(LowStockAlertStatuses.Ordered);

        await using var ordering = h.Ordering();
        var persisted = await ordering.Orders.SingleAsync(o => o.Id == order.Id);
        using var provenance = JsonDocument.Parse(persisted.ProvenanceJson);
        provenance.RootElement.GetProperty("alertIds").EnumerateArray().Single().GetGuid().Should().Be(alert.Id);
    }

    [Fact]
    public async Task CreateFromShortfall_Should_NotRefreshOrReopenTheOrderedAlert_OnASubsequentScan()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var rice = StockItemRef.Ingredient(riceId);
        await h.Inventory().SetOnHandAsync(rice, 2m);
        await h.Inventory().SetReorderPointAsync(rice, 30m);
        await h.Alerts().ScanAndRaiseAsync();

        await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));

        await using var commerce = h.Commerce();
        var ordered = await commerce.LowStockAlerts.SingleAsync();
        var snapshotBefore = (ordered.AvailableAtRaise, ordered.ReorderPoint, ordered.RaisedAt);

        // Stock is still breaching, but Ordered has LEFT the active set (Spec 052 §10): the next
        // scan must not refresh it and must not flip it back to Open — it is never "re-raised".
        // (Per landed Spec 052 semantics the scan starts a NEW cycle with a fresh Open alert while
        // stock stays low; suppressing that until receipt is a Spec 054 concern, not a status.)
        var rescan = await h.Alerts().ScanAndRaiseAsync();

        rescan.Refreshed.Should().Be(0);
        await using var commerce2 = h.Commerce();
        var orderedAfter = await commerce2.LowStockAlerts.SingleAsync(a => a.Id == ordered.Id);
        orderedAfter.Status.Should().Be(LowStockAlertStatuses.Ordered);
        (orderedAfter.AvailableAtRaise, orderedAfter.ReorderPoint, orderedAfter.RaisedAt).Should().Be(snapshotBefore);
    }

    [Fact]
    public async Task CreateFromShortfall_Should_UseReorderQuantityAsIs_WhenSet()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var rice = StockItemRef.Ingredient(riceId);
        await h.Inventory().SetOnHandAsync(rice, 2m);
        // The operator's explicit suggestion (60 kg) wins over pack rounding — taken as-is.
        await h.Inventory().SetReorderPointAsync(rice, 30m, reorderQuantity: 60m);
        await h.Alerts().ScanAndRaiseAsync();

        var order = await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));

        order.Items.Single().Quantity.Should().Be(60m);
        order.Items.Single().AmountIn.Should().Be(60_000m);
    }

    [Fact]
    public async Task CreateFromShortfall_Should_OrderMinimumOnePack_WhenShortfallIsZero()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var rice = StockItemRef.Ingredient(riceId);
        // Available exactly AT the reorder point: the alert fires (<=) with a zero shortfall —
        // the seed still orders one whole pack rather than a nonsensical zero-quantity line.
        await h.Inventory().SetOnHandAsync(rice, 30m);
        await h.Inventory().SetReorderPointAsync(rice, 30m);
        (await h.Alerts().ScanAndRaiseAsync()).Raised.Should().Be(1);

        var order = await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));

        order.Items.Single().Quantity.Should().Be(25m); // one 25 kg pack
    }

    [Fact]
    public async Task CreateFromShortfall_Auto_Should_SelectOnlyAlerts_ThisSupplierCanSupply()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var beans = await h.SeedIngredientAsync("Beans"); // low stock too, but NOT in this supplier's catalog

        await h.Inventory().SetOnHandAsync(StockItemRef.Ingredient(riceId), 2m);
        await h.Inventory().SetReorderPointAsync(StockItemRef.Ingredient(riceId), 30m);
        await h.Inventory().SetOnHandAsync(StockItemRef.Ingredient(beans), 1m);
        await h.Inventory().SetReorderPointAsync(StockItemRef.Ingredient(beans), 10m);
        (await h.Alerts().ScanAndRaiseAsync()).Raised.Should().Be(2);

        var order = await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));

        order.Items.Should().ContainSingle(i => i.ProductId == riceId);

        await using var commerce = h.Commerce();
        (await commerce.LowStockAlerts.SingleAsync(a => a.IngredientId == riceId)).Status.Should().Be(LowStockAlertStatuses.Ordered);
        (await commerce.LowStockAlerts.SingleAsync(a => a.IngredientId == beans)).Status.Should().Be(LowStockAlertStatuses.Open);
    }

    [Fact]
    public async Task CreateFromShortfall_Should_Reject_NamedAlert_ForIngredientWithoutACatalogRow()
    {
        var h = new Harness();
        var (supplierId, _) = await h.SeedSupplierWithRiceAsync();
        var beans = await h.SeedIngredientAsync("Beans");
        await h.Inventory().SetOnHandAsync(StockItemRef.Ingredient(beans), 1m);
        await h.Inventory().SetReorderPointAsync(StockItemRef.Ingredient(beans), 10m);
        await h.Alerts().ScanAndRaiseAsync();

        await using var commerce = h.Commerce();
        var beansAlert = await commerce.LowStockAlerts.SingleAsync(a => a.IngredientId == beans);

        // No catalog row → no PackSize to round with and no defensible price (no Spec 051 fallback).
        var act = async () => await h.PurchaseOrders().CreateFromShortfallAsync(
            new CreateFromShortfallCommand(supplierId, new[] { beansAlert.Id }));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'Beans'*");

        // Rejected seed leaves the alert untouched.
        await using var commerce2 = h.Commerce();
        (await commerce2.LowStockAlerts.SingleAsync(a => a.Id == beansAlert.Id)).Status.Should().Be(LowStockAlertStatuses.Open);
    }

    [Fact]
    public async Task CreateFromShortfall_Should_Reject_WhenNoActiveAlertThisSupplierCanSupply()
    {
        var h = new Harness();
        var (supplierId, _) = await h.SeedSupplierWithRiceAsync(); // stock never seeded → no alerts

        var act = async () => await h.PurchaseOrders().CreateFromShortfallAsync(new CreateFromShortfallCommand(supplierId));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No active*");
    }

    // ── §13 lifecycle guards (service-enforced over the guard-free spine) ───────────────────────

    [Fact]
    public async Task Submit_Should_TransitionDraftToPending_WithTheSubmittedReason()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var order = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }));

        var submitted = await h.PurchaseOrders().SubmitAsync(order.Id);

        submitted.Status.Should().Be(OrderStatusCodes.Pending);
        await using var ordering = h.Ordering();
        (await ordering.OrderHistoryEvents.CountAsync(e => e.OrderId == order.Id && e.EventType == "StatusChanged"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Submit_Should_Reject_WhenNotDraft()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();
        var order = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }));
        await h.PurchaseOrders().SubmitAsync(order.Id);

        var act = async () => await h.PurchaseOrders().SubmitAsync(order.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pending*only a Draft*");
    }

    [Fact]
    public async Task Submit_And_Cancel_Should_Reject_ANonPurchaseOrder()
    {
        var h = new Harness();
        // A ProductPurchase order on the same spine — PO operations must refuse to touch it.
        var retail = await h.Orders().CreateAsync(new CreateOrderCommand(
            OrderTypeCodes.ProductPurchase, Guid.NewGuid(), "NGN",
            new[] { new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 5_000m, "NGN") }));

        var submit = async () => await h.PurchaseOrders().SubmitAsync(retail.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a purchase order*");

        var cancel = async () => await h.PurchaseOrders().CancelAsync(retail.Id);
        await cancel.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a purchase order*");
    }

    [Fact]
    public async Task Cancel_Should_Work_FromDraft_AndFromPending_ButNotBeyond()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();

        // From Draft.
        var draft = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }));
        (await h.PurchaseOrders().CancelAsync(draft.Id, "changed plans")).Status.Should().Be(OrderStatusCodes.Cancelled);

        // From Pending (submitted, not yet received).
        var pending = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 50m) }));
        await h.PurchaseOrders().SubmitAsync(pending.Id);
        (await h.PurchaseOrders().CancelAsync(pending.Id)).Status.Should().Be(OrderStatusCodes.Cancelled);

        // Not from Cancelled (terminal)…
        var again = async () => await h.PurchaseOrders().CancelAsync(pending.Id);
        await again.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cancelled*");

        // …and not from Complete (fully received — Spec 054's transition, simulated via the spine).
        var complete = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 75m) }));
        await h.Orders().TransitionAsync(complete.Id, OrderStatusCodes.Complete, "received in full");
        var afterComplete = async () => await h.PurchaseOrders().CancelAsync(complete.Id);
        await afterComplete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Complete*");
    }

    [Fact]
    public async Task List_Should_ReturnOnlyPurchaseOrders_AndFilterByStatus()
    {
        var h = new Harness();
        var (supplierId, riceId) = await h.SeedSupplierWithRiceAsync();

        var a = await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 25m) }));
        await h.PurchaseOrders().CreateAsync(new CreatePurchaseOrderCommand(
            supplierId, new[] { new PurchaseOrderLineCommand(riceId, 50m) }));
        await h.Orders().CreateAsync(new CreateOrderCommand( // retail noise on the same spine
            OrderTypeCodes.ProductPurchase, Guid.NewGuid(), "NGN",
            new[] { new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 5_000m, "NGN") }));
        await h.PurchaseOrders().SubmitAsync(a.Id);

        var all = await h.PurchaseOrders().ListAsync();
        all.TotalCount.Should().Be(2);
        all.Items.Should().OnlyContain(o => o.OrderType == OrderTypeCodes.PurchaseOrder);

        var drafts = await h.PurchaseOrders().ListAsync(OrderStatusCodes.Draft);
        drafts.TotalCount.Should().Be(1);

        var pending = await h.PurchaseOrders().ListAsync(OrderStatusCodes.Pending);
        pending.Items.Single().Id.Should().Be(a.Id);
    }
}
