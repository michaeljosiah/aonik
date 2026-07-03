using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Production;
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
/// Production planning — the production sheet and the ingredient prep list (Spec 055). Pure
/// read/aggregation over the real CoreOrderService (per the PurchaseOrderServiceTests pattern):
/// the §9 inclusion filter (ProductPurchase only; committed statuses — never Draft/Cancelled;
/// CreatedAt in the half-open [FromUtc, ToUtc)), bundle-line expansion through
/// OrderBundleSelection (Spec 042 §12 Option A), the §10 explosion into Spec 050's worked
/// example, and §11 netting against Available = OnHand − Reserved (never raw on-hand) with the
/// Spec 053 seed precedence for the suggested order quantity.
/// </summary>
public class ProductionPlanningServiceTests
{
    private static readonly DateTime FromUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);
    private static ProductionWindow Window => new(FromUtc, ToUtc);

    private sealed class Harness
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _commerceDb = $"plan_c_{Guid.NewGuid()}";
        private readonly string _orderingDb = $"plan_o_{Guid.NewGuid()}";
        private readonly TestTenantProvider _tenant;
        private readonly TestCurrentUserProvider _user = new();

        public Harness() => _tenant = new TestTenantProvider(_tenantId);

        public Guid TenantId => _tenantId;

        /// <summary>Settable clock shared with the Ordering context so Order.CreatedAt — the §9
        /// window field — is test-controlled instead of wall-clock.</summary>
        public CommerceTestHarness.TestClock Clock { get; } = new();

        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options, _tenantId);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options,
            _tenant, _user, Clock);

        public CoreOrderService Orders() => new(Ordering(), _tenant, Clock, _user);

        public RecipeService Recipes() => new(Commerce(), _tenant);

        public InventoryService Inventory() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, Clock);

        public SupplierService Suppliers() => new(Commerce(), _tenant);

        public ProductionPlanningService Planning() => new(Commerce(), Orders(), Recipes(), Inventory(), _tenant);

        public async Task<Guid> SeedVariantAsync(string productName, string variantName)
        {
            await using var ctx = Commerce();
            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Slug = $"{productName.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
                Name = productName,
                Kind = ProductKinds.Simple,
            };
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ProductId = product.Id,
                Sku = $"SKU-{Guid.NewGuid():N}",
                Name = variantName,
            };
            ctx.Products.Add(product);
            ctx.ProductVariants.Add(variant);
            await ctx.SaveChangesAsync();
            return variant.Id;
        }

        public async Task<Guid> SeedIngredientAsync(string name)
        {
            await using var ctx = Commerce();
            var id = Guid.NewGuid();
            ctx.Ingredients.Add(new Ingredient
            {
                Id = id,
                TenantId = _tenantId,
                Name = name,
                BaseUnit = IngredientBaseUnits.Kg,
                IsActive = true,
            });
            await ctx.SaveChangesAsync();
            return id;
        }

        /// <summary>A ProductPurchase order on the spine with one retail line per (variant, qty),
        /// created at <paramref name="createdAtUtc"/> and optionally transitioned (checkout leaves
        /// Draft; payment completion drives Complete — Spec 042 §11).</summary>
        public async Task<OrderDto> CreateProductPurchaseAsync(
            DateTime createdAtUtc, string? transitionTo, params (Guid VariantId, decimal Quantity)[] lines)
        {
            Clock.UtcNow = createdAtUtc;
            var items = lines
                .Select((line, index) => new OrderItemCommand(
                    ItemType: OrderTypeCodes.ProductPurchase,
                    ItemIndex: index,
                    AmountIn: line.Quantity * 1_000m,
                    CurrencyIn: "NGN",
                    Quantity: line.Quantity,
                    UnitPrice: 1_000m,
                    ProductId: line.VariantId,
                    Sku: $"LINE-{index}"))
                .ToList();

            var order = await Orders().CreateAsync(new CreateOrderCommand(
                OrderType: OrderTypeCodes.ProductPurchase,
                PayerPartyId: Guid.NewGuid(),
                CurrencyIn: "NGN",
                Items: items));

            if (transitionTo is not null)
            {
                order = await Orders().TransitionAsync(order.Id, transitionTo, "test transition");
            }
            return order;
        }

        /// <summary>Commerce-owned bundle contents for one order line (Spec 042 §12 Option A) —
        /// Quantity is the line TOTAL (selection × boxes), exactly as checkout writes it.</summary>
        public async Task SeedBundleSelectionsAsync(
            Guid orderId, int orderItemIndex, params (Guid VariantId, decimal Quantity)[] selections)
        {
            await using var ctx = Commerce();
            foreach (var selection in selections)
            {
                ctx.OrderBundleSelections.Add(new OrderBundleSelection
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    OrderId = orderId,
                    OrderItemIndex = orderItemIndex,
                    BundleSlotId = Guid.NewGuid(),
                    ProductVariantId = selection.VariantId,
                    Quantity = selection.Quantity,
                    Sku = "SEL",
                });
            }
            await ctx.SaveChangesAsync();
        }
    }

    // ── §9 — the production sheet: aggregation, window boundaries, status filter ────────────────

    [Fact]
    public async Task GetProductionSheet_Should_SumPortionsByVariant_AndRespectWindowAndStatusFilter()
    {
        var h = new Harness();
        var jollof = await h.SeedVariantAsync("Jollof rice", "Jollof — regular");
        var steak = await h.SeedVariantAsync("Steak", "Steak — regular");

        // Counted: two Complete jollof orders (one ON the inclusive lower bound) and a Pending
        // steak order (committed-to-fulfil counts, not just paid-Complete).
        await h.CreateProductPurchaseAsync(FromUtc, OrderStatusCodes.Complete, (jollof, 24m));
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(3), OrderStatusCodes.Complete, (jollof, 16m));
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(4), OrderStatusCodes.Pending, (steak, 30m));

        // Excluded: created ON the exclusive upper bound; created before the window; a Cancelled
        // order in-window; a Draft (unpaid checkout intent) in-window.
        await h.CreateProductPurchaseAsync(ToUtc, OrderStatusCodes.Complete, (jollof, 5m));
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(-1), OrderStatusCodes.Complete, (jollof, 7m));
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(2), OrderStatusCodes.Cancelled, (jollof, 9m));
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(2), transitionTo: null, (steak, 11m));

        var sheet = await h.Planning().GetProductionSheetAsync(Window);

        sheet.Lines.Should().HaveCount(2);
        var jollofLine = sheet.Lines.Single(l => l.ProductVariantId == jollof);
        jollofLine.PortionsDemanded.Should().Be(40m);   // 24 + 16 — boundary/status rejects never leak in
        jollofLine.OrderCount.Should().Be(2);
        jollofLine.ProductName.Should().Be("Jollof rice");
        jollofLine.VariantName.Should().Be("Jollof — regular");

        var steakLine = sheet.Lines.Single(l => l.ProductVariantId == steak);
        steakLine.PortionsDemanded.Should().Be(30m);
        steakLine.OrderCount.Should().Be(1);

        sheet.TotalOrders.Should().Be(3);
        sheet.BundleLinesExpanded.Should().Be(0);
    }

    [Fact]
    public async Task GetProductionSheet_Should_ExpandBundleLines_ThroughOrderBundleSelections()
    {
        var h = new Harness();
        var jollof = await h.SeedVariantAsync("Jollof rice", "Jollof — regular");
        var steak = await h.SeedVariantAsync("Steak", "Steak — regular");
        var bundleProductId = Guid.NewGuid(); // the order line carries the BUNDLE PRODUCT id (042 §12 Option A)

        // One order: line 0 = a build-your-own-box (2 boxes), line 1 = a simple steak line.
        var order = await h.CreateProductPurchaseAsync(
            FromUtc.AddDays(1), OrderStatusCodes.Complete, (bundleProductId, 2m), (steak, 1m));
        // Chosen contents of line 0, quantities already the line totals (2 jollof + 1 steak per box × 2).
        await h.SeedBundleSelectionsAsync(order.Id, 0, (jollof, 4m), (steak, 2m));

        var sheet = await h.Planning().GetProductionSheetAsync(Window);

        // The bundle line was expanded: components carry the demand, the bundle product never does.
        sheet.Lines.Should().HaveCount(2);
        sheet.Lines.Should().NotContain(l => l.ProductVariantId == bundleProductId);
        sheet.Lines.Single(l => l.ProductVariantId == jollof).PortionsDemanded.Should().Be(4m);
        var steakLine = sheet.Lines.Single(l => l.ProductVariantId == steak);
        steakLine.PortionsDemanded.Should().Be(3m);     // 2 from the box contents + 1 simple line
        steakLine.OrderCount.Should().Be(1);            // same order, counted once
        sheet.BundleLinesExpanded.Should().Be(1);
        sheet.TotalOrders.Should().Be(1);
    }

    [Fact]
    public async Task GetProductionSheet_Should_KeepUnresolvedVariantLines_WithDiagnosticNames()
    {
        var h = new Harness();
        var ghostVariantId = Guid.NewGuid(); // never in the catalog (deleted / foreign id)

        await h.CreateProductPurchaseAsync(FromUtc.AddDays(1), OrderStatusCodes.Complete, (ghostVariantId, 5m));

        var sheet = await h.Planning().GetProductionSheetAsync(Window);

        // LEFT-join semantics: the demand still shows — with diagnostic names — never dropped.
        var line = sheet.Lines.Should().ContainSingle().Subject;
        line.ProductVariantId.Should().Be(ghostVariantId);
        line.PortionsDemanded.Should().Be(5m);
        line.ProductName.Should().Be("(unknown product)");
        line.VariantName.Should().Be("(unknown variant)");
    }

    // ── §10 — the prep list: Spec 050's worked example ──────────────────────────────────────────

    [Fact]
    public async Task GetPrepList_Should_MatchSpec050WorkedExample_WhenNotNetting()
    {
        var h = new Harness();
        var jollof = await h.SeedVariantAsync("Jollof rice", "Jollof — regular");
        var steak = await h.SeedVariantAsync("Steak", "Steak — regular");
        var rice = await h.SeedIngredientAsync("Rice");
        var tomato = await h.SeedIngredientAsync("Tomato");
        var beef = await h.SeedIngredientAsync("Beef");

        // The Spec 050 recipe economics: jollof yields 4 portions from 1 kg rice + 0.5 kg tomato.
        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice, 1m),
            new RecipeComponentCommand(tomato, 0.5m),
        }));
        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(steak, "Steak", 1m, "portion", new[]
        {
            new RecipeComponentCommand(beef, 0.3m),
        }));

        await h.CreateProductPurchaseAsync(FromUtc.AddDays(1), OrderStatusCodes.Complete, (jollof, 40m));
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(2), OrderStatusCodes.Complete, (steak, 30m));

        var prepList = await h.Planning().GetPrepListAsync(Window, netAgainstStock: false);

        // 40 jollof + 30 steak ⇒ 10 kg rice, 5 kg tomato, 9 kg beef — Spec 050's day-in-the-life.
        prepList.Lines.Should().HaveCount(3);
        var riceLine = prepList.Lines.Single(l => l.IngredientId == rice);
        riceLine.RequiredQuantity.Should().Be(10m);
        riceLine.IngredientName.Should().Be("Rice");
        riceLine.BaseUnit.Should().Be(IngredientBaseUnits.Kg);
        prepList.Lines.Single(l => l.IngredientId == tomato).RequiredQuantity.Should().Be(5m);
        prepList.Lines.Single(l => l.IngredientId == beef).RequiredQuantity.Should().Be(9m);

        // Raw requirements: no netting fields at all.
        prepList.NettedAgainstStock.Should().BeFalse();
        prepList.Lines.Should().OnlyContain(l =>
            l.Available == null && l.Shortfall == null && l.SuggestedOrderQuantity == null);
        prepList.VariantsWithoutRecipe.Should().BeEmpty();
        prepList.Window.Should().Be(Window);
    }

    [Fact]
    public async Task GetPrepList_Should_SurfaceVariantsWithoutRecipe_NeverSilentlyUnderCount()
    {
        var h = new Harness();
        var jollof = await h.SeedVariantAsync("Jollof rice", "Jollof — regular");
        var steak = await h.SeedVariantAsync("Steak", "Steak — regular"); // deliberately no recipe
        var rice = await h.SeedIngredientAsync("Rice");

        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice, 1m),
        }));

        await h.CreateProductPurchaseAsync(FromUtc.AddDays(1), OrderStatusCodes.Complete, (jollof, 8m), (steak, 30m));

        var prepList = await h.Planning().GetPrepListAsync(Window, netAgainstStock: false);

        prepList.Lines.Should().ContainSingle(l => l.IngredientId == rice)
            .Which.RequiredQuantity.Should().Be(2m);
        prepList.VariantsWithoutRecipe.Should().ContainSingle().Which.Should().Be(steak);
    }

    // ── §11 — netting against Available (OnHand − Reserved), never raw on-hand ──────────────────

    [Fact]
    public async Task GetPrepList_Should_NetAgainstAvailable_NotRawOnHand_AndSuggestPerSeedPrecedence()
    {
        var h = new Harness();
        var jollof = await h.SeedVariantAsync("Jollof rice", "Jollof — regular");
        var rice = await h.SeedIngredientAsync("Rice");
        var tomato = await h.SeedIngredientAsync("Tomato");
        var pepper = await h.SeedIngredientAsync("Pepper");
        var salt = await h.SeedIngredientAsync("Salt");

        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice, 1m),
            new RecipeComponentCommand(tomato, 0.5m),
            new RecipeComponentCommand(pepper, 0.1m),
            new RecipeComponentCommand(salt, 0.05m),
        }));

        // 20 portions ⇒ rice 5 kg, tomato 2.5 kg, pepper 0.5 kg, salt 0.25 kg required.
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(1), OrderStatusCodes.Complete, (jollof, 20m));

        // Rice — the Codex example: 10 on hand but 8 reserved ⇒ Available 2, shortfall 3 (NOT 0,
        // the false "in stock" netting raw on-hand would report).
        var inventory = h.Inventory();
        await inventory.SetOnHandAsync(StockItemRef.Ingredient(rice), 10m);
        await inventory.ReserveAsync(Guid.NewGuid(), new[]
        {
            new InventoryReservationLine(StockItemRef.Ingredient(rice), 8m),
        });
        // A 25 kg-sack catalog row (active supplier) ⇒ the 3 kg shortfall rounds up to one pack.
        var supplier = await h.Suppliers().CreateAsync(new CreateSupplierCommand("Mama Nkechi Farms", "NGN", null));
        await h.Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplier.Id, rice, PackSize: 25m, PackPrice: 25_000m, Sku: "SUP-RICE-25"));

        // Tomato — the operator's explicit ReorderQuantity wins, taken as-is (053 seed precedence).
        await inventory.SetReorderPointAsync(StockItemRef.Ingredient(tomato), reorderPoint: 1m, reorderQuantity: 12m);

        // Pepper — no stock row, no reorder quantity, no catalog row ⇒ suggestion just covers the gap.
        // Salt — plenty available ⇒ no shortfall, no suggestion.
        await inventory.SetOnHandAsync(StockItemRef.Ingredient(salt), 50m);

        var prepList = await h.Planning().GetPrepListAsync(Window);

        prepList.NettedAgainstStock.Should().BeTrue();

        var riceLine = prepList.Lines.Single(l => l.IngredientId == rice);
        riceLine.RequiredQuantity.Should().Be(5m);
        riceLine.Available.Should().Be(2m);             // OnHand 10 − Reserved 8
        riceLine.Shortfall.Should().Be(3m);             // 5 − 2, never max(5 − 10, 0) = 0
        riceLine.SuggestedOrderQuantity.Should().Be(25m); // one whole 25 kg sack

        var tomatoLine = prepList.Lines.Single(l => l.IngredientId == tomato);
        tomatoLine.Available.Should().Be(0m);
        tomatoLine.Shortfall.Should().Be(2.5m);
        tomatoLine.SuggestedOrderQuantity.Should().Be(12m); // ReorderQuantity as-is

        var pepperLine = prepList.Lines.Single(l => l.IngredientId == pepper);
        pepperLine.Available.Should().Be(0m);
        pepperLine.Shortfall.Should().Be(0.5m);
        pepperLine.SuggestedOrderQuantity.Should().Be(0.5m); // fallback: cover the gap

        var saltLine = prepList.Lines.Single(l => l.IngredientId == salt);
        saltLine.Available.Should().Be(50m);
        saltLine.Shortfall.Should().Be(0m);
        saltLine.SuggestedOrderQuantity.Should().BeNull();   // nothing to order

        // The same window with netAgainstStock: false stays a pure requirements list even though
        // stock rows exist.
        var raw = await h.Planning().GetPrepListAsync(Window, netAgainstStock: false);
        raw.Lines.Should().OnlyContain(l =>
            l.Available == null && l.Shortfall == null && l.SuggestedOrderQuantity == null);
    }

    [Fact]
    public async Task GetPrepList_Should_RoundToCheapestPack_OnlyWhenCatalogRowsShareOneCurrency()
    {
        var h = new Harness();
        var jollof = await h.SeedVariantAsync("Jollof rice", "Jollof — regular");
        var rice = await h.SeedIngredientAsync("Rice");
        var tomato = await h.SeedIngredientAsync("Tomato");

        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice, 1m),
            new RecipeComponentCommand(tomato, 0.5m),
        }));

        // 12 portions ⇒ rice 3 kg, tomato 1.5 kg required; nothing stocked ⇒ shortfall = required.
        await h.CreateProductPurchaseAsync(FromUtc.AddDays(1), OrderStatusCodes.Complete, (jollof, 12m));

        // Rice: two NGN rows — 25 kg @ ₦30,000 (₦1,200/kg) vs 10 kg @ ₦8,000 (₦800/kg): the
        // CHEAPEST per base unit wins, so 3 kg rounds up to one 10 kg pack, not a 25 kg sack.
        var supplierA = await h.Suppliers().CreateAsync(new CreateSupplierCommand("Sack Traders", "NGN", null));
        var supplierB = await h.Suppliers().CreateAsync(new CreateSupplierCommand("Bulk Grains", "NGN", null));
        await h.Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplierA.Id, rice, PackSize: 25m, PackPrice: 30_000m));
        await h.Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplierB.Id, rice, PackSize: 10m, PackPrice: 8_000m));

        // Tomato: rows in NGN and GBP — Commerce holds no FX, so "cheapest" is unrankable across
        // currencies; pack rounding is skipped and the suggestion falls back to the raw shortfall.
        var supplierC = await h.Suppliers().CreateAsync(new CreateSupplierCommand("London Produce", "GBP", null));
        await h.Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplierB.Id, tomato, PackSize: 5m, PackPrice: 5_000m));
        await h.Suppliers().UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplierC.Id, tomato, PackSize: 20m, PackPrice: 100m));

        var prepList = await h.Planning().GetPrepListAsync(Window);

        prepList.Lines.Single(l => l.IngredientId == rice).SuggestedOrderQuantity.Should().Be(10m);
        prepList.Lines.Single(l => l.IngredientId == tomato).SuggestedOrderQuantity.Should().Be(1.5m);
    }

    // ── §12 — window validation ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Planning_Should_RejectInvalidWindows()
    {
        var h = new Harness();

        var inverted = () => h.Planning().GetProductionSheetAsync(new ProductionWindow(ToUtc, FromUtc));
        await inverted.Should().ThrowAsync<ArgumentException>().WithMessage("*FromUtc < ToUtc*");

        var empty = () => h.Planning().GetProductionSheetAsync(new ProductionWindow(FromUtc, FromUtc));
        await empty.Should().ThrowAsync<ArgumentException>().WithMessage("*FromUtc < ToUtc*");

        // The prep list applies the same guard (it IS the sheet, exploded).
        var tooWide = () => h.Planning().GetPrepListAsync(new ProductionWindow(FromUtc, FromUtc.AddDays(93)));
        await tooWide.Should().ThrowAsync<ArgumentException>().WithMessage("*92 days*");
    }
}
