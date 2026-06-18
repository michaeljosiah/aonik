using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Product + bundle pricing (Spec 042 §9/§12), including the build-your-own-box scenario:
/// a custom wellness food box assembled from component products and bought as one unit.
/// </summary>
public class ProductPricingServiceTests
{
    private static ProductService Products(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, new TestTenantProvider(tenantId));

    private static ProductPricingService Pricing(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, new TestTenantProvider(tenantId), new CommerceTestHarness.TestClock());

    [Fact]
    public async Task SetPrice_Then_ResolvePrice_Should_ReturnActiveAmount()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var products = Products(ctx, tenantId);
        var pricing = Pricing(ctx, tenantId);

        var product = await products.CreateProductAsync(new CreateProductCommand(
            "tea", "Wellness Tea", ProductKinds.Variant,
            Variants: new[] { new CreateVariantLine("TEA-20", "20 bags") }));
        var variantId = product.Variants.Single().Id;

        await pricing.SetPriceAsync(new SetPriceCommand(variantId, "NGN", 2_500m));
        // A second set supersedes the first.
        await pricing.SetPriceAsync(new SetPriceCommand(variantId, "NGN", 3_000m));

        (await pricing.ResolvePriceAsync(variantId, "NGN")).Should().Be(3_000m);
        (await pricing.ResolvePriceAsync(variantId, "USD")).Should().BeNull();
    }

    [Fact]
    public async Task ResolveBundlePrice_Fixed_Should_ReturnBoxPrice_ForValidSelection()
    {
        // "Build your own box: pick any 6 items for ₦12,000."
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var products = Products(ctx, tenantId);
        var pricing = Pricing(ctx, tenantId);

        var category = await products.CreateCategoryAsync(new CreateCategoryCommand("granola", "Granola"));

        // 8 granola component variants.
        var variantIds = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            var p = await products.CreateProductAsync(new CreateProductCommand(
                $"granola-{i}", $"Granola {i}", ProductKinds.Simple, CategoryId: category.Id,
                Variants: new[] { new CreateVariantLine($"GRAN-{i}", $"Granola {i} 500g") }));
            variantIds.Add(p.Variants.Single().Id);
        }

        // The bundle: fixed ₦12,000 for a 6-item box drawn from the granola category.
        var box = await products.CreateProductAsync(new CreateProductCommand(
            "wellness-box", "Build Your Own Wellness Box", ProductKinds.Bundle,
            BundlePricingMode: BundlePricingModes.Fixed,
            BundleFixedAmount: 12_000m,
            BundleCurrency: "NGN"));
        var slot = await products.AddBundleSlotAsync(new AddBundleSlotCommand(
            box.Id, "Pick 6 granolas", MinItems: 6, MaxItems: 6, FromCategoryId: category.Id));

        var selection = variantIds.Take(6)
            .Select(v => new BundleSelectionLine(slot.Id, v))
            .ToList();

        var price = await pricing.ResolveBundlePriceAsync(box.Id, selection, "NGN");
        price.Should().Be(12_000m);

        // Picking only 5 violates the slot's Min/Max.
        var tooFew = variantIds.Take(5).Select(v => new BundleSelectionLine(slot.Id, v)).ToList();
        var act = async () => await pricing.ResolveBundlePriceAsync(box.Id, tooFew, "NGN");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveBundlePrice_SumOfComponents_Should_SumChosenPrices()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var products = Products(ctx, tenantId);
        var pricing = Pricing(ctx, tenantId);

        var category = await products.CreateCategoryAsync(new CreateCategoryCommand("snacks", "Snacks"));

        var v = new List<Guid>();
        foreach (var (slug, price) in new[] { ("bar", 1_000m), ("nuts", 2_000m), ("chips", 1_500m) })
        {
            var p = await products.CreateProductAsync(new CreateProductCommand(
                slug, slug, ProductKinds.Simple, CategoryId: category.Id,
                Variants: new[] { new CreateVariantLine($"SNK-{slug}", slug) }));
            var variantId = p.Variants.Single().Id;
            await pricing.SetPriceAsync(new SetPriceCommand(variantId, "NGN", price));
            v.Add(variantId);
        }

        var box = await products.CreateProductAsync(new CreateProductCommand(
            "snack-box", "Snack Box", ProductKinds.Bundle,
            BundlePricingMode: BundlePricingModes.SumOfComponents));
        var slot = await products.AddBundleSlotAsync(new AddBundleSlotCommand(
            box.Id, "Pick 2-3 snacks", MinItems: 2, MaxItems: 3));

        // bar (1000) + nuts (2000) = 3000
        var selection = new[]
        {
            new BundleSelectionLine(slot.Id, v[0]),
            new BundleSelectionLine(slot.Id, v[1]),
        };

        (await pricing.ResolveBundlePriceAsync(box.Id, selection, "NGN")).Should().Be(3_000m);
    }
}
