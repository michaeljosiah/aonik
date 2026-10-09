using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using CatalogEntities = Aonik.Commerce.Entities.Catalog;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 068 scaffolding: a size-tiered bundle with the launch pricing table (6–30, base 95.00,
/// 15.00/space, preset 12 → 170.00), a category-sourced slot, personalisable dishes with stock,
/// and every service wired the CheckoutServiceTests way (fresh context per call, shared store).
/// </summary>
internal sealed class BoxTestHarness
{
    private readonly string _commerceDb = $"TestDb_{Guid.NewGuid()}";
    private readonly string _orderingDb = $"TestDb_{Guid.NewGuid()}";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TestTenantProvider _tenant;
    private readonly TestCurrentUserProvider _user = new();
    private readonly CommerceTestHarness.TestClock _clock = new();

    public BoxTestHarness() => _tenant = new TestTenantProvider(_tenantId);

    public Guid TenantId => _tenantId;
    public CommerceTestHarness.TestClock Clock => _clock;

    public FakeBoxPaymentInitiator Payments { get; } = new();
    public GuestOrderAccess GuestOrderAccess { get; } = new(new EphemeralDataProtectionProvider());

    public static CheckoutDeliveryDetails ValidDelivery => new(
        new CheckoutContactDto("purchaser@example.test", "Pat", "Customer", "+44 7700 900123"),
        new DeliveryAddressDto("1 Test Street", null, "London", null, "SW1A 1AA", "GB"),
        new DateOnly(2026, 6, 25));

    /// <summary>Tenant-scoped delivery settings for quote/checkout tests; empty = defaults (0/0).</summary>
    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);

    public CommerceDbContext Commerce(params IInterceptor[] interceptors) => CommerceTestHarness.CreateContext(
        new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb)
            .AddInterceptors(interceptors).Options, _tenantId, _clock);

    public OrderingDbContext Ordering() => new(
        new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options, _tenant, _user);

    public ProductService Products()
    {
        var ctx = Commerce();
        return new(ctx, _tenant, CommerceTestHarness.NewOptionService(ctx, _tenantId), NullLogger<ProductService>.Instance,
            CommerceTestHarness.NewContentService(ctx, _tenantId));
    }

    public ProductPricingService Pricing() => new(Commerce(), _tenant, _clock);
    public InventoryService Inventory() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, _clock);
    public CartService Carts() => new(Commerce(), _tenant, Pricing(), _clock);
    public async Task<CartAccessContext> HoldDeliveryAsync(Guid cartId, CartAccessContext access)
    {
        await using var context = Commerce();
        var hold = await new DeliveryReservationService(context, _tenant, _clock)
            .ReserveAsync(cartId, ValidDelivery.DeliveryDate, access);
        return access with { ExpectedCartVersion = hold.CartVersion };
    }

    public async Task<BoxCartDto> HoldDeliveryAsync(BoxCartDto box)
    {
        var access = await HoldDeliveryAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken, box.CartVersion));
        return box with { CartVersion = access.ExpectedCartVersion! };
    }
    public BundleSizePlanService Plans() => new(Commerce(), _tenant);
    public StorefrontOrderService StorefrontOrders() => new(
        Commerce(), _tenant, new CoreOrderService(Ordering(), _tenant, _clock, _user), GuestOrderAccess);

    public BoxCartService BoxCarts(CommerceDbContext? context = null)
    {
        var ctx = context ?? Commerce();
        return new(ctx, _tenant, CommerceTestHarness.NewSelectionService(ctx, _tenantId), Inventory(),
            new DictionaryTenantSettingStore(Settings), new NullSettingProvider(), new GbpTenantCurrencyProvider(), Pricing(), _clock);
    }

    /// <summary>CheckoutService and its IBoxCheckoutSupport share ONE context, exactly as the
    /// scoped production registration resolves them — the drift repair mutates entities the
    /// checkout context tracks, so a split pair would silently save nothing.</summary>
    public CheckoutService Checkout(IDeliveryCoverageService? coverage = null)
    {
        var ctx = Commerce();
        var inventory = new InventoryService(ctx, _tenant, new TenantContext { TenantId = _tenantId }, _clock);
        var boxCarts = new BoxCartService(ctx, _tenant,
            CommerceTestHarness.NewSelectionService(ctx, _tenantId), inventory,
            new DictionaryTenantSettingStore(Settings), new NullSettingProvider(), new GbpTenantCurrencyProvider(), Pricing(), _clock);
        return new CheckoutService(
            ctx, inventory, new CoreOrderService(Ordering(), _tenant, _clock, _user),
            Payments, new FakeBoxInvoiceWriter(), new DiscountService(ctx, _tenant, _clock),
            new ZeroRateTaxCalculator(), _tenant, boxCarts, GuestOrderAccess,
            new FulfilmentPromiseService(ctx, _tenant, _clock), coverage ?? new ServedTestDeliveryCoverage(), CommerceTestHarness.Parties(), _clock);
    }

    public CartMaintenanceService Maintenance() => new(
        Commerce(), new TenantContext { TenantId = _tenantId }, new NullSettingProvider(), _clock);

    // ─── The standard fixture ────────────────────────────────────────────────

    public sealed record BoxFixture(
        Guid BundleProductId,
        Guid SlotId,
        Guid CategoryId,
        IReadOnlyDictionary<string, Guid> DishVariants,
        IReadOnlyDictionary<string, Guid> DishProducts,
        OptionCatalogueBuilder Options);

    /// <summary>Launch table plan + one "Pick dishes" slot sourcing a dishes category + N dishes,
    /// each personalisable via the standard option catalogue, stocked at 10.</summary>
    public async Task<BoxFixture> BuildAsync(params string[] dishSlugs)
    {
        var products = Products();
        var category = await products.CreateCategoryAsync(new CreateCategoryCommand("dishes", "Dishes"));

        var ctx = Commerce();
        var builder = new OptionCatalogueBuilder(ctx, _tenantId);
        ctx.FulfilmentCalendars.Add(new FulfilmentCalendar
        {
            TenantId = _tenantId, Timezone = "Europe/London", IsActive = true,
            DeliveryDaysJson = "[\"thursday\"]", LeadDays = 7,
            CutoffLocalTime = new TimeOnly(23, 59)
        });
        ctx.DeliveryDateCapacities.Add(new DeliveryDateCapacity
        {
            TenantId = _tenantId, DeliveryDate = ValidDelivery.DeliveryDate, Unit = "box", Capacity = 100
        });
        await ctx.SaveChangesAsync();
        await builder.BuildCatalogueAsync();

        var variants = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var dishProducts = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var slug in dishSlugs)
        {
            var dish = await products.CreateProductAsync(new CreateProductCommand(
                slug, slug, CatalogEntities.ProductKinds.Simple, CategoryId: category.Id,
                Variants: new[] { new CreateVariantLine($"SKU-{slug}", slug) }));
            var variantId = dish.Variants.Single().Id;
            variants[slug] = variantId;
            dishProducts[slug] = dish.Id;
            await Inventory().SetOnHandAsync(variantId, 10m);
            await builder.OfferAllAsync(dish.Id);
        }

        var bundle = await products.CreateProductAsync(new CreateProductCommand(
            "meal-box", "Meal Box", CatalogEntities.ProductKinds.Bundle,
            BundlePricingMode: CatalogEntities.BundlePricingModes.SizeTiered));
        // A meal box legitimately repeats dishes (6 × jollof) — AbbysTable's slot is authored
        // duplicate-friendly; the restrictive-slot rules get their own dedicated test.
        var slot = await products.AddBundleSlotAsync(new AddBundleSlotCommand(
            bundle.Id, "Pick dishes", MinItems: 0, MaxItems: 99, FromCategoryId: category.Id,
            AllowDuplicates: true));

        await Plans().UpsertAsync(bundle.Id, new UpsertBundleSizePlanCommand(
            MinSize: 6, MaxSize: 30, BaseSize: 6, BasePrice: 95m, PerSpacePrice: 15m, Currency: "GBP",
            Presets: new[] { new BundleSizePresetCommand(12, 170m, Badge: "Most popular") }));

        return new BoxFixture(bundle.Id, slot.Id, category.Id, variants, dishProducts, builder);
    }

    /// <summary>Spec 071 — an extra: an ordinary Simple product with a GBP retail price, stock,
    /// and membership of the "extras" collection. Returns (productId, variantId).</summary>
    public async Task<(Guid ProductId, Guid VariantId)> AddExtraAsync(
        string slug, decimal price, bool inExtrasCollection = true, decimal stock = 50m)
    {
        var products = Products();
        var extra = await products.CreateProductAsync(new CreateProductCommand(
            slug, slug, CatalogEntities.ProductKinds.Simple,
            Variants: new[] { new CreateVariantLine($"SKU-{slug}", slug) }));
        var variantId = extra.Variants.Single().Id;
        if (price > 0)
        {
            await Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", price));
        }
        await Inventory().SetOnHandAsync(variantId, stock);

        if (inExtrasCollection)
        {
            await using var ctx = Commerce();
            var collection = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(ctx.Collections, c => c.Slug == "extras");
            if (collection is null)
            {
                collection = new CatalogEntities.Collection
                {
                    Id = Guid.NewGuid(), TenantId = _tenantId, Slug = "extras", Title = "Extras",
                    Kind = CatalogEntities.CollectionKinds.Curated, IsActive = true,
                };
                ctx.Collections.Add(collection);
            }
            var rank = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .CountAsync(ctx.CollectionItems, i => i.CollectionId == collection.Id);
            ctx.CollectionItems.Add(new CatalogEntities.CollectionItem
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, CollectionId = collection.Id,
                ProductId = extra.Id, Rank = rank,
            });
            await ctx.SaveChangesAsync();
        }

        return (extra.Id, variantId);
    }

    public ExtrasCatalogService Extras()
    {
        var ctx = Commerce();
        return new ExtrasCatalogService(ctx,
            _tenant,
            new DictionaryTenantSettingStore(Settings),
            new NullSettingProvider(),
            new GbpTenantCurrencyProvider(),
            new ProductPricingService(ctx, _tenant, _clock),
            CommerceTestHarness.NewOptionService(ctx, _tenantId),
            CommerceTestHarness.NewContentService(ctx, _tenantId));
    }
}

internal sealed class DictionaryTenantSettingStore : Aonik.SharedKernel.Abstractions.Settings.ITenantSettingStore
{
    private readonly Dictionary<string, string> _values;

    public DictionaryTenantSettingStore(Dictionary<string, string> values) => _values = values;

    public Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeBoxPaymentInitiator : TestPaymentState
{
    public decimal LastAmount { get; private set; }
    public int Calls { get; private set; }

    public override Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken ct = default)
    {
        Calls++;
        LastAmount = command.Amount;
        return Task.FromResult(Record(command, "secret_box", "https://pay.example/box"));
    }
}

internal sealed class FakeBoxInvoiceWriter : IInvoiceWriter
{
    public Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken ct = default)
        => Task.FromResult(new InvoiceRef(Guid.NewGuid(), "INV-BOX", command.Lines.Sum(l => l.Quantity * l.UnitPrice), command.Currency));
}
