using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
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
using Microsoft.Extensions.Logging.Abstractions;

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

    public FakeBoxPaymentInitiator Payments { get; } = new();

    /// <summary>Tenant-scoped delivery settings for quote/checkout tests; empty = defaults (0/0).</summary>
    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);

    public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(
        new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options, _tenantId);

    public OrderingDbContext Ordering() => new(
        new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options, _tenant, _user);

    public ProductService Products()
    {
        var ctx = Commerce();
        return new(ctx, _tenant, CommerceTestHarness.NewOptionService(ctx, _tenantId), NullLogger<ProductService>.Instance);
    }

    public ProductPricingService Pricing() => new(Commerce(), _tenant, _clock);
    public InventoryService Inventory() => new(Commerce(), _tenant, new TenantContext { TenantId = _tenantId }, _clock);
    public CartService Carts() => new(Commerce(), _tenant, Pricing());
    public BundleSizePlanService Plans() => new(Commerce(), _tenant);

    public BoxCartService BoxCarts()
    {
        var ctx = Commerce();
        return new(ctx, _tenant, CommerceTestHarness.NewSelectionService(ctx, _tenantId), Inventory(),
            new DictionaryTenantSettingStore(Settings), new NullSettingProvider());
    }

    /// <summary>CheckoutService and its IBoxCheckoutSupport share ONE context, exactly as the
    /// scoped production registration resolves them — the drift repair mutates entities the
    /// checkout context tracks, so a split pair would silently save nothing.</summary>
    public CheckoutService Checkout()
    {
        var ctx = Commerce();
        var inventory = new InventoryService(ctx, _tenant, new TenantContext { TenantId = _tenantId }, _clock);
        var boxCarts = new BoxCartService(ctx, _tenant,
            CommerceTestHarness.NewSelectionService(ctx, _tenantId), inventory,
            new DictionaryTenantSettingStore(Settings), new NullSettingProvider());
        return new CheckoutService(
            ctx, inventory, new CoreOrderService(Ordering(), _tenant, _clock, _user),
            Payments, new FakeBoxInvoiceWriter(), new DiscountService(ctx, _tenant, _clock),
            new ZeroRateTaxCalculator(), _tenant, boxCarts);
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
        var slot = await products.AddBundleSlotAsync(new AddBundleSlotCommand(
            bundle.Id, "Pick dishes", MinItems: 0, MaxItems: 99, FromCategoryId: category.Id));

        await Plans().UpsertAsync(bundle.Id, new UpsertBundleSizePlanCommand(
            MinSize: 6, MaxSize: 30, BaseSize: 6, BasePrice: 95m, PerSpacePrice: 15m, Currency: "GBP",
            Presets: new[] { new BundleSizePresetCommand(12, 170m, Badge: "Most popular") }));

        return new BoxFixture(bundle.Id, slot.Id, category.Id, variants, dishProducts, builder);
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

internal sealed class FakeBoxPaymentInitiator : IPaymentInitiator
{
    public decimal LastAmount { get; private set; }
    public int Calls { get; private set; }

    public Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken ct = default)
    {
        Calls++;
        LastAmount = command.Amount;
        return Task.FromResult(new PaymentIntentRef(Guid.NewGuid(), "Pending", "secret_box", "https://pay.example/box"));
    }
}

internal sealed class FakeBoxInvoiceWriter : IInvoiceWriter
{
    public Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken ct = default)
        => Task.FromResult(new InvoiceRef(Guid.NewGuid(), "INV-BOX", command.Lines.Sum(l => l.Quantity * l.UnitPrice), command.Currency));
}
