using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Catalog management over the Commerce module (Spec 042 §8/§12).</summary>
public class ProductServiceTests
{
    [Fact]
    public async Task CreateProductAsync_Should_PersistProduct_WithVariants()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);

        var created = await service.CreateProductAsync(new CreateProductCommand(
            Slug: "granola-vanilla",
            Name: "Vanilla Granola",
            Kind: ProductKinds.Variant,
            Variants: new[]
            {
                new CreateVariantLine("GRAN-VAN-500", "500g", null, 500m),
                new CreateVariantLine("GRAN-VAN-1KG", "1kg", null, 1000m),
            }));

        created.Kind.Should().Be(ProductKinds.Variant);
        created.Variants.Should().HaveCount(2);
        created.Variants.Select(v => v.Sku).Should().Contain(new[] { "GRAN-VAN-500", "GRAN-VAN-1KG" });

        // Persisted, not just tracked.
        await using var ctx2 = CommerceTestHarness.CreateContext(options, tenantId);
        var fetched = await CommerceTestHarness.NewProductService(ctx2, tenantId)
            .GetProductBySlugAsync("granola-vanilla");
        fetched.Should().NotBeNull();
        fetched!.Variants.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateProductAsync_Should_RejectDuplicateSlug()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);

        await service.CreateProductAsync(new CreateProductCommand("dup", "One", ProductKinds.Simple));
        var act = async () => await service.CreateProductAsync(new CreateProductCommand("dup", "Two", ProductKinds.Simple));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ListProductsAsync_Should_FilterByKind_AndPage()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);

        await service.CreateProductAsync(new CreateProductCommand("box", "Wellness Box", ProductKinds.Bundle));
        await service.CreateProductAsync(new CreateProductCommand("a", "Apple", ProductKinds.Simple));
        await service.CreateProductAsync(new CreateProductCommand("b", "Banana", ProductKinds.Simple));

        var bundles = await service.ListProductsAsync(new ListProductsQuery(Kind: ProductKinds.Bundle));
        bundles.TotalCount.Should().Be(1);
        bundles.Items.Should().OnlyContain(p => p.Kind == ProductKinds.Bundle);

        var simples = await service.ListProductsAsync(new ListProductsQuery(Kind: ProductKinds.Simple));
        simples.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task AddBundleSlotAsync_Should_RejectWhenProductNotBundle()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);

        var simple = await service.CreateProductAsync(new CreateProductCommand("plain", "Plain", ProductKinds.Simple));
        var act = async () => await service.AddBundleSlotAsync(new AddBundleSlotCommand(simple.Id, "Slot", 1, 3));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
