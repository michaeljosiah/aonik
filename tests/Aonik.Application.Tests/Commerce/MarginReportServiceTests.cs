using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Reporting;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Production;
using Aonik.Commerce.Services.Promotions;
using Aonik.Commerce.Services.Reporting;
using Aonik.Commerce.Services.Sourcing;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

using static Aonik.Application.Tests.Commerce.CartTestAccess;

/// <summary>
/// The margin &amp; profit report (Spec 057). Composes the REAL services end to end — checkout
/// (CheckoutService over the real CoreOrderService, with the CheckoutServiceTests payment/invoice
/// test doubles) fabricates the paid orders and their durable OrderChargeSummary rows exactly as
/// production does; ConfirmPaymentAsync applies the payment-completed transition. Covers: the §8
/// revenue-inclusion rule (payment-completed Complete orders only — narrower than Spec 055's
/// demand set: Draft/Pending/Cancelled and out-of-window or other-currency orders never count);
/// hand-computed revenue/COGS/margin on the Spec 051 jollof economics (₦400/portion); the §8
/// pro-rata discount apportionment reconciling exactly to the discounted total; bundle-line
/// component expansion (bundle id never a row); the unknown-COGS row surfaced and EXCLUDED from
/// the aggregate (never zero cost — the P1 fix); and the §10 target-margin flag + set/clear
/// validation.
/// </summary>
public class MarginReportServiceTests
{
    private static readonly DateTime FromUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);
    private static ProductionWindow Window => new(FromUtc, ToUtc);

    private sealed class FakePaymentInitiator : IPaymentInitiator
    {
        public Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken ct = default)
            => Task.FromResult(new PaymentIntentRef(Guid.NewGuid(), "Pending", "secret_123", "https://pay.example/checkout"));
    }

    private sealed class FakeInvoiceWriter : IInvoiceWriter
    {
        public Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken ct = default)
            => Task.FromResult(new InvoiceRef(Guid.NewGuid(), "INV-TEST", command.Lines.Sum(l => l.Quantity * l.UnitPrice), command.Currency));
    }

    private sealed class Harness
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _commerceDb = $"margin_c_{Guid.NewGuid()}";
        private readonly string _orderingDb = $"margin_o_{Guid.NewGuid()}";
        private readonly TestTenantProvider _tenant;
        private readonly TestCurrentUserProvider _user = new();

        public Harness() => _tenant = new TestTenantProvider(_tenantId);

        public Guid TenantId => _tenantId;

        /// <summary>Shared with the Ordering context so Order.CreatedAt — the §8 window field —
        /// and ingredient-cost effective dates are test-controlled instead of wall-clock. The
        /// default (2026-06-18) predates the window, so costs seeded before orders are effective
        /// throughout.</summary>
        public CommerceTestHarness.TestClock Clock { get; } = new();

        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options, _tenantId, Clock);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options,
            _tenant, _user, Clock);

        public CoreOrderService Orders() => new(Ordering(), _tenant, Clock, _user);

        public ProductService Products()
        {
            // ProductService and its Spec 066 option dependency must share one context.
            var ctx = Commerce();
            return new(ctx, _tenant, new ProductOptionService(ctx, _tenant, new ProductContentReviewFlagger(ctx), NullLogger<ProductOptionService>.Instance), NullLogger<ProductService>.Instance);
        }
        public ProductPricingService Pricing() => new(Commerce(), _tenant, Clock);
        public InventoryService Inventory() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, Clock);
        public CartService Carts() => new(Commerce(), _tenant, Pricing());
        public DiscountService Discounts() => new(Commerce(), _tenant, Clock);
        public RecipeService Recipes() => new(Commerce(), _tenant);
        public IngredientCostService Costs() => new(Commerce(), _tenant, Clock);
        public ProductCostingService Costing() => new(Recipes(), Costs(), Clock);

        public CheckoutService Checkout()
        {
            var ctx = Commerce();
            var boxCarts = new BoxCartService(ctx, _tenant,
                CommerceTestHarness.NewSelectionService(ctx, _tenantId), Inventory(),
                new NullTenantSettingStore(), new NullSettingProvider(), new GbpTenantCurrencyProvider(), new ProductPricingService(ctx, _tenant, Clock));
            return new CheckoutService(
                Commerce(), Inventory(), Orders(), new FakePaymentInitiator(), new FakeInvoiceWriter(),
                Discounts(), new ZeroRateTaxCalculator(), _tenant, boxCarts);
        }

        public MarginReportService Margins() => new(Commerce(), Orders(), Costing(), Pricing(), _tenant);

        /// <summary>Report evaluated at the window end so every seeded cost is effective (the
        /// rollup is date-aware) — deterministic regardless of the last order's clock.</summary>
        public Task<MarginReportDto> ReportAsync(string currency = "NGN")
        {
            Clock.UtcNow = ToUtc;
            return Margins().GetMarginReportAsync(Window, currency);
        }

        public async Task<(Guid ProductId, Guid VariantId)> SeedSimpleAsync(
            string name, decimal priceNgn, Guid? categoryId = null)
        {
            var product = await Products().CreateProductAsync(new CreateProductCommand(
                $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}", name, ProductKinds.Simple,
                CategoryId: categoryId,
                Variants: new[] { new CreateVariantLine($"SKU-{Guid.NewGuid():N}", name) }));
            var variantId = product.Variants.Single().Id;
            await Pricing().SetPriceAsync(new SetPriceCommand(variantId, "NGN", priceNgn));
            await Inventory().SetOnHandAsync(variantId, 1_000m);
            return (product.Id, variantId);
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

        /// <summary>The Spec 051 jollof economics: 1 kg rice (₦1,200) + 0.5 kg tomato (₦800)
        /// yields 4 portions ⇒ standard cost ₦400/portion.</summary>
        public async Task GiveJollofEconomicsAsync(Guid variantId)
        {
            var rice = await SeedIngredientAsync($"Rice {Guid.NewGuid():N}");
            var tomato = await SeedIngredientAsync($"Tomato {Guid.NewGuid():N}");
            await Recipes().SetRecipeAsync(new SetRecipeCommand(variantId, "Jollof rice", 4m, "portion", new[]
            {
                new RecipeComponentCommand(rice, 1m),
                new RecipeComponentCommand(tomato, 0.5m),
            }));
            await Costs().SetCostAsync(new SetIngredientCostCommand(rice, "NGN", 1_200m));
            await Costs().SetCostAsync(new SetIngredientCostCommand(tomato, "NGN", 800m));
        }

        /// <summary>0.3 kg beef (₦5,000/kg) yields 1 portion ⇒ standard cost ₦1,500/portion.</summary>
        public async Task GiveSteakEconomicsAsync(Guid variantId)
        {
            var beef = await SeedIngredientAsync($"Beef {Guid.NewGuid():N}");
            await Recipes().SetRecipeAsync(new SetRecipeCommand(variantId, "Steak", 1m, "portion", new[]
            {
                new RecipeComponentCommand(beef, 0.3m),
            }));
            await Costs().SetCostAsync(new SetIngredientCostCommand(beef, "NGN", 5_000m));
        }

        /// <summary>A checkout-created ProductPurchase order, exactly as production fabricates it
        /// (order + charge summary + bundle selections), optionally payment-confirmed — the
        /// ConfirmPaymentAsync path that transitions the order to Complete (§8).</summary>
        public async Task<CheckoutResult> CheckoutAsync(
            DateTime createdAtUtc, bool confirmPayment, string currency = "NGN", string? discountCode = null,
            params (Guid VariantId, decimal Quantity)[] lines)
        {
            Clock.UtcNow = createdAtUtc;
            var cart = await Carts().CreateCartAsync(new CreateCartCommand(currency, BuyerPartyId: Guid.NewGuid()));
            foreach (var (variantId, quantity) in lines)
            {
                await Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, quantity), Owner(cart));
            }
            var result = await Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card", DiscountCode: discountCode), Owner(cart));
            if (confirmPayment)
            {
                await Checkout().ConfirmPaymentAsync(result.OrderId);
            }
            return result;
        }
    }

    // ── happy path: hand-computed revenue / COGS / margin per row + aggregate ───────────────────

    [Fact]
    public async Task GetMarginReport_Should_ComputeRevenueCogsAndMargin_PerVariantAndAggregate()
    {
        var h = new Harness();
        var (_, jollof) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m);
        var (_, steak) = await h.SeedSimpleAsync("Steak", priceNgn: 3_000m);
        await h.GiveJollofEconomicsAsync(jollof);   // ₦400/portion
        await h.GiveSteakEconomicsAsync(steak);     // ₦1,500/portion

        await h.CheckoutAsync(FromUtc.AddDays(1), confirmPayment: true, lines: (jollof, 40m));
        await h.CheckoutAsync(FromUtc.AddDays(2), confirmPayment: true, lines: (steak, 30m));

        var report = await h.ReportAsync();

        report.Currency.Should().Be("NGN");
        report.Window.Should().Be(Window);
        report.Rows.Should().HaveCount(2);

        // Jollof: 40 × ₦2,000 = ₦80,000 revenue; COGS 40 × ₦400 = ₦16,000; margin ₦64,000 = 80%.
        var jollofRow = report.Rows.Single(r => r.ProductVariantId == jollof);
        jollofRow.ProductName.Should().Be("Jollof rice");
        jollofRow.QuantitySold.Should().Be(40m);
        jollofRow.Revenue.Should().Be(80_000m);
        jollofRow.CogsKnown.Should().BeTrue();
        jollofRow.Cogs.Should().Be(16_000m);
        jollofRow.GrossMargin.Should().Be(64_000m);
        jollofRow.MarginPct.Should().Be(80.00m);
        jollofRow.IsBundle.Should().BeFalse();
        jollofRow.TargetMarginPct.Should().BeNull();
        jollofRow.BelowTarget.Should().BeNull();    // no target set ⇒ never flagged

        // Steak: 30 × ₦3,000 = ₦90,000; COGS 30 × ₦1,500 = ₦45,000; margin ₦45,000 = 50%.
        var steakRow = report.Rows.Single(r => r.ProductVariantId == steak);
        steakRow.QuantitySold.Should().Be(30m);
        steakRow.Revenue.Should().Be(90_000m);
        steakRow.Cogs.Should().Be(45_000m);
        steakRow.MarginPct.Should().Be(50.00m);

        // Aggregate over both rows: ₦170,000 revenue − ₦61,000 COGS = ₦109,000 ⇒ 64.12%.
        report.Aggregate.Revenue.Should().Be(170_000m);
        report.Aggregate.KnownCogsRevenue.Should().Be(170_000m);
        report.Aggregate.Cogs.Should().Be(61_000m);
        report.Aggregate.GrossMargin.Should().Be(109_000m);
        report.Aggregate.MarginPct.Should().Be(64.12m);
        report.Aggregate.UnknownCogsRevenue.Should().Be(0m);
        report.VariantsWithoutRecipe.Should().BeEmpty();
        report.VariantsWithUnknownCost.Should().BeEmpty();
        report.OrdersExcludedByCurrency.Should().Be(0);
    }

    // ── §9/R5 — unknown COGS is null, surfaced, and EXCLUDED from the aggregate (the P1 fix) ─────

    [Fact]
    public async Task GetMarginReport_Should_ExcludeUnknownCogsRowsFromAggregate_NeverZeroCost()
    {
        var h = new Harness();
        var (_, jollof) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m);
        var (_, mystery) = await h.SeedSimpleAsync("Mystery dish", priceNgn: 1_000m);   // no recipe at all
        var (_, saffronRice) = await h.SeedSimpleAsync("Saffron rice", priceNgn: 4_000m);
        await h.GiveJollofEconomicsAsync(jollof);
        // Saffron rice HAS a recipe, but its component has no cost in NGN — the Spec 051
        // missing-cost diagnostic (CostComplete = false).
        var saffron = await h.SeedIngredientAsync("Saffron");
        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(saffronRice, "Saffron rice", 1m, "portion", new[]
        {
            new RecipeComponentCommand(saffron, 0.2m),
        }));

        await h.CheckoutAsync(FromUtc.AddDays(1), confirmPayment: true,
            lines: [(jollof, 10m), (mystery, 5m), (saffronRice, 2m)]);

        var report = await h.ReportAsync();

        // The unknown-COGS rows surface with nulls — never a phantom zero cost / 100% margin.
        var mysteryRow = report.Rows.Single(r => r.ProductVariantId == mystery);
        mysteryRow.Revenue.Should().Be(5_000m);
        mysteryRow.CogsKnown.Should().BeFalse();
        mysteryRow.Cogs.Should().BeNull();
        mysteryRow.GrossMargin.Should().BeNull();
        mysteryRow.MarginPct.Should().BeNull();

        var saffronRow = report.Rows.Single(r => r.ProductVariantId == saffronRice);
        saffronRow.Revenue.Should().Be(8_000m);
        saffronRow.CogsKnown.Should().BeFalse();
        saffronRow.Cogs.Should().BeNull();

        // Diagnostics distinguish no-recipe from recipe-with-missing-cost.
        report.VariantsWithoutRecipe.Should().ContainSingle().Which.Should().Be(mystery);
        report.VariantsWithUnknownCost.Should().ContainSingle().Which.Should().Be(saffronRice);

        // The aggregate covers COGS-known rows ONLY: jollof's ₦20,000 revenue / ₦4,000 COGS ⇒ 80%.
        report.Aggregate.Revenue.Should().Be(33_000m);              // all rows
        report.Aggregate.KnownCogsRevenue.Should().Be(20_000m);     // jollof only
        report.Aggregate.Cogs.Should().Be(4_000m);
        report.Aggregate.GrossMargin.Should().Be(16_000m);
        report.Aggregate.MarginPct.Should().Be(80.00m);
        report.Aggregate.UnknownCogsRevenue.Should().Be(13_000m);   // surfaced, excluded

        // The P1 fix, asserted: had the unknown rows been folded in at ZERO cost, the aggregate
        // would report (33,000 − 4,000) / 33,000 = 87.88% — inflated profit. It must not.
        var inflatedIfZeroed = Math.Round(
            (report.Aggregate.Revenue - report.Aggregate.Cogs) / report.Aggregate.Revenue * 100m,
            2, MidpointRounding.AwayFromZero);
        inflatedIfZeroed.Should().Be(87.88m);
        report.Aggregate.MarginPct.Should().NotBe(inflatedIfZeroed);
    }

    // ── §8 — bundle lines expand into components; the bundle id is never a row ───────────────────

    [Fact]
    public async Task GetMarginReport_Should_ExpandBundleLines_AllocatingRevenueByComponentValue()
    {
        var h = new Harness();
        var category = await h.Products().CreateCategoryAsync(new CreateCategoryCommand("mains", "Mains"));
        var (_, jollof) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m, category.Id);
        var (_, moimoi) = await h.SeedSimpleAsync("Moi moi", priceNgn: 1_000m, category.Id);
        await h.GiveJollofEconomicsAsync(jollof);   // moi moi deliberately has no recipe

        // A fixed-price box (₦4,500 < the ₦5,000 component value — the box discount).
        var box = await h.Products().CreateProductAsync(new CreateProductCommand(
            "family-box", "Family Box", ProductKinds.Bundle,
            BundlePricingMode: BundlePricingModes.Fixed, BundleFixedAmount: 4_500m, BundleCurrency: "NGN"));
        var slot = await h.Products().AddBundleSlotAsync(new AddBundleSlotCommand(
            box.Id, "Pick 3", MinItems: 3, MaxItems: 3, FromCategoryId: category.Id));

        h.Clock.UtcNow = FromUtc.AddDays(1);
        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("NGN", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddBundleAsync(new AddBundleToCartCommand(cart.Id, box.Id, new[]
        {
            new BundleSelectionLine(slot.Id, jollof, 2m),
            new BundleSelectionLine(slot.Id, moimoi, 1m),
        }), Owner(cart));
        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        await h.Checkout().ConfirmPaymentAsync(result.OrderId);

        var report = await h.ReportAsync();

        // Component value (list price × qty): jollof ₦4,000, moi moi ₦1,000 ⇒ the ₦4,500 line
        // splits 3,600 / 900 — and the bundle product id itself never appears as a row.
        report.Rows.Should().HaveCount(2);
        report.Rows.Should().NotContain(r => r.ProductVariantId == box.Id);

        var jollofRow = report.Rows.Single(r => r.ProductVariantId == jollof);
        jollofRow.IsBundle.Should().BeTrue();
        jollofRow.QuantitySold.Should().Be(2m);
        jollofRow.Revenue.Should().Be(3_600m);
        jollofRow.Cogs.Should().Be(800m);                   // 2 × ₦400
        jollofRow.GrossMargin.Should().Be(2_800m);
        jollofRow.MarginPct.Should().Be(77.78m);            // 2,800 / 3,600

        var moimoiRow = report.Rows.Single(r => r.ProductVariantId == moimoi);
        moimoiRow.IsBundle.Should().BeTrue();
        moimoiRow.QuantitySold.Should().Be(1m);
        moimoiRow.Revenue.Should().Be(900m);
        moimoiRow.CogsKnown.Should().BeFalse();

        // Component revenue reconciles exactly to the funded bundle line.
        report.Rows.Sum(r => r.Revenue).Should().Be(4_500m);
        report.Aggregate.Revenue.Should().Be(4_500m);
        report.Aggregate.KnownCogsRevenue.Should().Be(3_600m);
        report.Aggregate.UnknownCogsRevenue.Should().Be(900m);
    }

    // ── the report currency is normalized ONCE (051 convention) and used everywhere ──────────────

    [Fact]
    public async Task GetMarginReport_Should_NormalizeCurrency_KeepingValueWeightedBundleSplit()
    {
        var h = new Harness();
        var category = await h.Products().CreateCategoryAsync(new CreateCategoryCommand("mains", "Mains"));
        var (_, jollof) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m, category.Id);
        var (_, moimoi) = await h.SeedSimpleAsync("Moi moi", priceNgn: 1_000m, category.Id);
        await h.GiveJollofEconomicsAsync(jollof);

        // UNEQUAL component prices: the value split (4,000 : 1,000 ⇒ 3,600 / 900) and the
        // quantity fallback (2 : 1 ⇒ 3,000 / 1,500) visibly disagree.
        var box = await h.Products().CreateProductAsync(new CreateProductCommand(
            "family-box", "Family Box", ProductKinds.Bundle,
            BundlePricingMode: BundlePricingModes.Fixed, BundleFixedAmount: 4_500m, BundleCurrency: "NGN"));
        var slot = await h.Products().AddBundleSlotAsync(new AddBundleSlotCommand(
            box.Id, "Pick 3", MinItems: 3, MaxItems: 3, FromCategoryId: category.Id));

        h.Clock.UtcNow = FromUtc.AddDays(1);
        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("NGN", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddBundleAsync(new AddBundleToCartCommand(cart.Id, box.Id, new[]
        {
            new BundleSelectionLine(slot.Id, jollof, 2m),
            new BundleSelectionLine(slot.Id, moimoi, 1m),
        }), Owner(cart));
        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        await h.Checkout().ConfirmPaymentAsync(result.OrderId);

        // A lowercase report currency must behave IDENTICALLY to the uppercase call. The order
        // filter admits case-insensitively but ResolvePriceAsync matches ProductPrice.Currency
        // exactly — an un-normalized "ngn" would find the order, miss every component's standalone
        // NGN price, and silently degrade the §8 value-weighted split to the quantity fallback.
        var lower = await h.ReportAsync("ngn");
        var upper = await h.ReportAsync("NGN");

        lower.Currency.Should().Be("NGN");                  // the DTO echoes the NORMALIZED currency
        lower.OrdersExcludedByCurrency.Should().Be(0);
        var jollofRow = lower.Rows.Single(r => r.ProductVariantId == jollof);
        jollofRow.Revenue.Should().Be(3_600m);              // value-weighted, NOT the 3,000 fallback
        jollofRow.Cogs.Should().Be(800m);                   // the rollup got the normalized currency too
        lower.Rows.Single(r => r.ProductVariantId == moimoi).Revenue.Should().Be(900m);
        lower.Should().BeEquivalentTo(upper);
    }

    // ── §8 — whole-order discount apportioned pro-rata; Σ rows == discounted total EXACTLY ───────

    [Fact]
    public async Task GetMarginReport_Should_ApportionOrderDiscount_AndReconcileExactly()
    {
        var h = new Harness();
        var (_, akara) = await h.SeedSimpleAsync("Akara", priceNgn: 1_000m);
        var (_, puffpuff) = await h.SeedSimpleAsync("Puff puff", priceNgn: 1_000m);
        var (_, zobo) = await h.SeedSimpleAsync("Zobo", priceNgn: 1_000m);
        await h.Discounts().CreateAsync(new CreateDiscountCommand("SAVE100", DiscountKinds.FixedAmount, 100m, "NGN"));

        // One order, three ₦1,000 lines, ₦100 whole-order discount: the ₦33.33̅ per-line share
        // does not round evenly at 4 dp — the remainder lands on the first (largest-tie) line.
        var result = await h.CheckoutAsync(FromUtc.AddDays(1), confirmPayment: true, discountCode: "SAVE100",
            lines: [(akara, 1m), (puffpuff, 1m), (zobo, 1m)]);

        result.Subtotal.Should().Be(3_000m);
        result.DiscountTotal.Should().Be(100m);

        var report = await h.ReportAsync();

        report.Rows.Should().HaveCount(3);
        report.Rows.Single(r => r.ProductVariantId == akara).Revenue.Should().Be(966.6666m);
        report.Rows.Single(r => r.ProductVariantId == puffpuff).Revenue.Should().Be(966.6667m);
        report.Rows.Single(r => r.ProductVariantId == zobo).Revenue.Should().Be(966.6667m);

        // Rounding reconciliation: per-variant revenue sums to the DISCOUNTED total exactly —
        // never the ₦3,000 list price.
        report.Rows.Sum(r => r.Revenue).Should().Be(2_900m);
        report.Aggregate.Revenue.Should().Be(2_900m);

        // No recipes anywhere ⇒ no COGS-known revenue: the aggregate margin is null, never 100%.
        report.Aggregate.KnownCogsRevenue.Should().Be(0m);
        report.Aggregate.MarginPct.Should().BeNull();
        report.Aggregate.UnknownCogsRevenue.Should().Be(2_900m);
    }

    // ── §8 — the revenue-inclusion rule: only payment-completed orders count ─────────────────────

    [Fact]
    public async Task GetMarginReport_Should_CountOnlyPaymentCompletedOrders_InWindowAndCurrency()
    {
        var h = new Harness();
        var (_, jollof) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m);
        await h.Pricing().SetPriceAsync(new SetPriceCommand(jollof, "GBP", 10m));

        // Counted: payment-completed (Complete) in the window.
        await h.CheckoutAsync(FromUtc.AddDays(1), confirmPayment: true, lines: (jollof, 3m));

        // Excluded — the profit-vs-demand distinction (§8): an unconfirmed checkout stays Draft
        // (unpaid intent); a Pending order is committed demand Spec 055 WOULD cook for, but no
        // money has arrived; a Cancelled order is terminal-failed.
        await h.CheckoutAsync(FromUtc.AddDays(2), confirmPayment: false, lines: (jollof, 11m));
        var pending = await h.CheckoutAsync(FromUtc.AddDays(3), confirmPayment: false, lines: (jollof, 13m));
        await h.Orders().TransitionAsync(pending.OrderId, OrderStatusCodes.Pending, "submitted");
        var cancelled = await h.CheckoutAsync(FromUtc.AddDays(4), confirmPayment: false, lines: (jollof, 17m));
        await h.Orders().TransitionAsync(cancelled.OrderId, OrderStatusCodes.Cancelled, "abandoned");

        // Excluded — paid but created ON the exclusive upper bound (half-open window).
        await h.CheckoutAsync(ToUtc, confirmPayment: true, lines: (jollof, 19m));

        // Skipped + surfaced — paid, in-window, but in another currency (Commerce holds no FX).
        await h.CheckoutAsync(FromUtc.AddDays(5), confirmPayment: true, currency: "GBP", lines: (jollof, 23m));

        var report = await h.ReportAsync("NGN");

        var row = report.Rows.Should().ContainSingle().Subject;
        row.ProductVariantId.Should().Be(jollof);
        row.QuantitySold.Should().Be(3m);       // only the paid, in-window, NGN order
        row.Revenue.Should().Be(6_000m);
        report.Aggregate.Revenue.Should().Be(6_000m);
        report.OrdersExcludedByCurrency.Should().Be(1);
    }

    // ── §10 — target margin: BelowTarget flags ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMarginReport_Should_FlagBelowTarget_OnlyWhenMarginAndTargetAreKnown()
    {
        var h = new Harness();
        var (jollofProduct, jollof) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m);   // 80% margin
        var (steakProduct, steak) = await h.SeedSimpleAsync("Steak", priceNgn: 3_000m);           // 50% margin
        var (mysteryProduct, mystery) = await h.SeedSimpleAsync("Mystery dish", priceNgn: 1_000m); // unknown COGS
        var (_, tea) = await h.SeedSimpleAsync("Tea", priceNgn: 1_000m);                          // no target
        await h.GiveJollofEconomicsAsync(jollof);
        await h.GiveSteakEconomicsAsync(steak);
        await h.GiveJollofEconomicsAsync(tea);

        await h.Margins().SetTargetMarginAsync(jollofProduct, 90m);   // above the achieved 80 ⇒ flagged
        await h.Margins().SetTargetMarginAsync(steakProduct, 40m);    // below the achieved 50 ⇒ ok
        await h.Margins().SetTargetMarginAsync(mysteryProduct, 50m);  // margin unknown ⇒ null flag

        await h.CheckoutAsync(FromUtc.AddDays(1), confirmPayment: true,
            lines: [(jollof, 4m), (steak, 2m), (mystery, 1m), (tea, 1m)]);

        var report = await h.ReportAsync();

        var jollofRow = report.Rows.Single(r => r.ProductVariantId == jollof);
        jollofRow.TargetMarginPct.Should().Be(90m);
        jollofRow.MarginPct.Should().Be(80.00m);
        jollofRow.BelowTarget.Should().BeTrue();

        var steakRow = report.Rows.Single(r => r.ProductVariantId == steak);
        steakRow.TargetMarginPct.Should().Be(40m);
        steakRow.BelowTarget.Should().BeFalse();

        // A target with UNKNOWN margin is never flagged (null, not false-positive)…
        var mysteryRow = report.Rows.Single(r => r.ProductVariantId == mystery);
        mysteryRow.CogsKnown.Should().BeFalse();
        mysteryRow.TargetMarginPct.Should().Be(50m);
        mysteryRow.BelowTarget.Should().BeNull();

        // …and a known margin with NO target reports the margin but is never flagged.
        var teaRow = report.Rows.Single(r => r.ProductVariantId == tea);
        teaRow.MarginPct.Should().NotBeNull();
        teaRow.TargetMarginPct.Should().BeNull();
        teaRow.BelowTarget.Should().BeNull();
    }

    [Fact]
    public async Task SetTargetMargin_Should_ValidateRange_RoundTo2dp_AndClearToNull()
    {
        var h = new Harness();
        var (productId, _) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m);

        // Range: a percentage on the 0–100 scale.
        var tooHigh = () => h.Margins().SetTargetMarginAsync(productId, 100.01m);
        await tooHigh.Should().ThrowAsync<ArgumentException>().WithMessage("*between 0 and 100*");
        var negative = () => h.Margins().SetTargetMarginAsync(productId, -0.5m);
        await negative.Should().ThrowAsync<ArgumentException>().WithMessage("*between 0 and 100*");

        var unknownProduct = () => h.Margins().SetTargetMarginAsync(Guid.NewGuid(), 50m);
        await unknownProduct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");

        // Stored at 2 dp (the column precision), away from zero.
        var set = await h.Margins().SetTargetMarginAsync(productId, 66.666m);
        set.ProductId.Should().Be(productId);
        set.TargetMarginPct.Should().Be(66.67m);
        await using (var ctx = h.Commerce())
        {
            (await ctx.Products.SingleAsync(p => p.Id == productId)).TargetMarginPct.Should().Be(66.67m);
        }

        // Null clears the target.
        var cleared = await h.Margins().SetTargetMarginAsync(productId, null);
        cleared.TargetMarginPct.Should().BeNull();
        await using (var ctx = h.Commerce())
        {
            (await ctx.Products.SingleAsync(p => p.Id == productId)).TargetMarginPct.Should().BeNull();
        }

        // The boundary values themselves are legal.
        (await h.Margins().SetTargetMarginAsync(productId, 0m)).TargetMarginPct.Should().Be(0m);
        (await h.Margins().SetTargetMarginAsync(productId, 100m)).TargetMarginPct.Should().Be(100m);
    }

    [Fact]
    public async Task SetTargetMargin_Should_BeExposedOnProductReads()
    {
        var h = new Harness();
        var (productId, _) = await h.SeedSimpleAsync("Jollof rice", priceNgn: 2_000m);

        // Freshly created — no target yet.
        (await h.Products().GetProductAsync(productId))!.TargetMarginPct.Should().BeNull();

        // Set → the full product read (the admin edit surface) carries it.
        await h.Margins().SetTargetMarginAsync(productId, 62.5m);
        (await h.Products().GetProductAsync(productId))!.TargetMarginPct.Should().Be(62.5m);

        // Clear → null again.
        await h.Margins().SetTargetMarginAsync(productId, null);
        (await h.Products().GetProductAsync(productId))!.TargetMarginPct.Should().BeNull();
    }

    // ── window validation (mirrors Spec 055 §12) ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMarginReport_Should_RejectInvalidWindowsAndMissingCurrency()
    {
        var h = new Harness();

        var inverted = () => h.Margins().GetMarginReportAsync(new ProductionWindow(ToUtc, FromUtc), "NGN");
        await inverted.Should().ThrowAsync<ArgumentException>().WithMessage("*FromUtc < ToUtc*");

        var tooWide = () => h.Margins().GetMarginReportAsync(new ProductionWindow(FromUtc, FromUtc.AddDays(93)), "NGN");
        await tooWide.Should().ThrowAsync<ArgumentException>().WithMessage("*92 days*");

        var noCurrency = () => h.Margins().GetMarginReportAsync(Window, " ");
        await noCurrency.Should().ThrowAsync<ArgumentException>().WithMessage("*currency*");
    }
}
