using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using NotFoundException = Aonik.SharedKernel.Abstractions.NotFoundException;

namespace Aonik.Application.Tests.Commerce;

public class ProductDishDataTests
{
    [Fact]
    public async Task Authoring_Should_PreserveUntouchedFacts_AndClearOnlyExplicitMembers()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new MerchandisingBuilder(ctx, tenantId);
        var relatedId = await builder.WithCollectionAsync("related");
        var created = await builder.Products.CreateProductAsync(new CreateProductCommand(
            "dish", "Dish", ProductKinds.Simple, Heat: 0, ComponentsLine: "  with rice  ",
            LowSugar: false, Freezable: true, ShelfLife: "  Three days chilled  ", RelatedCollectionId: relatedId));

        created.IsPlaceholder.Should().BeTrue();
        created.Heat.Should().Be(0);
        created.LowSugar.Should().BeFalse();
        created.ComponentsLine.Should().Be("with rice");
        created.RelatedCollectionSlug.Should().Be("related");

        var renamed = await builder.Products.UpdateProductAsync(created.Id, new UpdateProductCommand(Name: "Renamed"));
        renamed.Heat.Should().Be(0);
        renamed.LowSugar.Should().BeFalse();
        renamed.Freezable.Should().BeTrue();
        renamed.ShelfLife.Should().Be("Three days chilled");
        renamed.RelatedCollectionId.Should().Be(relatedId);
        renamed.IsPlaceholder.Should().BeTrue();

        await builder.Products.UpdateProductAsync(created.Id, new UpdateProductCommand(
            ClearHeat: true, ComponentsLine: " ", ClearLowSugar: true, ClearFreezable: true,
            ShelfLife: "", ClearRelatedCollection: true, IsPlaceholder: false));
        await using var read = CommerceTestHarness.CreateContext(options, tenantId);
        var saved = (await CommerceTestHarness.NewProductService(read, tenantId).GetAdminProductAsync(created.Id))!;
        saved.Heat.Should().BeNull();
        saved.ComponentsLine.Should().BeNull();
        saved.LowSugar.Should().BeNull();
        saved.Freezable.Should().BeNull();
        saved.ShelfLife.Should().BeNull();
        saved.RelatedCollectionId.Should().BeNull();
        saved.IsPlaceholder.Should().BeFalse();
    }

    [Fact]
    public async Task Authoring_Should_ValidateFactsBeforeChangingTrackedState()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);
        var product = await service.CreateProductAsync(new CreateProductCommand("dish", "Dish", ProductKinds.Simple));

        foreach (var invalid in new[]
        {
            new UpdateProductCommand(Name: "Changed", Heat: -1),
            new UpdateProductCommand(Name: "Changed", Heat: 4),
            new UpdateProductCommand(Name: "Changed", ComponentsLine: new string('x', 501)),
            new UpdateProductCommand(Name: "Changed", ShelfLife: new string('x', 1001)),
        })
        {
            await FluentActions.Awaiting(() => service.UpdateProductAsync(product.Id, invalid))
                .Should().ThrowAsync<StorefrontValidationException>();
        }
        await ctx.SaveChangesAsync();
        (await service.GetProductAsync(product.Id))!.Name.Should().Be("Dish");
        await FluentActions.Awaiting(() => service.CreateProductAsync(new CreateProductCommand(
            "invalid", "Invalid", ProductKinds.Simple, Heat: 4))).Should().ThrowAsync<StorefrontValidationException>();
        (await ctx.Products.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TagAuthoring_Should_RequireTenantVocabulary_WhilePreservingRetiredKeys()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);
        var facets = new FacetGroupService(ctx, new TestTenantProvider(tenantId));
        var definition = await facets.CreateAsync(new CreateFacetGroupCommand(
            "style", "Style", FacetMatchKinds.Tag, """[{"value":"protein-led","label":"Protein led"}]"""));
        var product = await service.CreateProductAsync(new CreateProductCommand(
            "dish", "Dish", ProductKinds.Simple, TagsJson: """["protein-led"]"""));
        await facets.UpdateAsync(definition.Id, new UpdateFacetGroupCommand("Style", IsActive: false));

        var unchanged = await service.UpdateProductAsync(product.Id, new UpdateProductCommand(
            Name: "Renamed", TagsJson: """["protein-led"]"""));
        unchanged.TagsJson.Should().Be("""["protein-led"]""");
        await FluentActions.Awaiting(() => service.UpdateProductAsync(product.Id, new UpdateProductCommand(
            TagsJson: """["protein-led","invented"]"""))).Should().ThrowAsync<StorefrontValidationException>();

        var foreignTenant = Guid.NewGuid();
        await using var other = CommerceTestHarness.CreateContext(options, foreignTenant);
        await new FacetGroupService(other, new TestTenantProvider(foreignTenant)).CreateAsync(new CreateFacetGroupCommand(
            "style", "Style", FacetMatchKinds.Tag, """[{"value":"foreign-only","label":"Other"}]"""));
        await FluentActions.Awaiting(() => service.CreateProductAsync(new CreateProductCommand(
            "foreign-tag", "Other", ProductKinds.Simple, TagsJson: """["foreign-only"]""")))
            .Should().ThrowAsync<StorefrontValidationException>();

        (await service.UpdateProductAsync(product.Id, new UpdateProductCommand(TagsJson: "[]")))
            .TagsJson.Should().Be("[]");
    }

    [Fact]
    public async Task RelatedCollection_Should_RejectForeignReferences_AndPublishOnlyActiveSlug()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new MerchandisingBuilder(ctx, tenantId);
        var relatedId = await builder.WithCollectionAsync("related");
        var category = await builder.Products.CreateCategoryAsync(new CreateCategoryCommand("lamb", "Lamb"));
        var product = await builder.Products.CreateProductAsync(new CreateProductCommand(
            "dish", "Dish", ProductKinds.Simple, CategoryId: category.Id, RelatedCollectionId: relatedId));
        product.CategoryName.Should().Be("Lamb");
        product.CategorySlug.Should().Be("lamb");
        product.RelatedCollectionSlug.Should().Be("related");
        await builder.Collections.UpdateAsync(relatedId, new UpdateCollectionCommand("Related", IsActive: false));
        (await builder.Products.GetProductBySlugAsync("dish"))!.RelatedCollectionSlug.Should().BeNull();
        (await builder.Products.GetAdminProductAsync(product.Id))!.RelatedCollectionId.Should().Be(relatedId);

        var foreignTenant = Guid.NewGuid();
        await using var other = CommerceTestHarness.CreateContext(options, foreignTenant);
        var foreignBuilder = new MerchandisingBuilder(other, foreignTenant);
        var foreignId = await foreignBuilder.WithCollectionAsync("private");
        var foreignCategory = await foreignBuilder.Products.CreateCategoryAsync(new CreateCategoryCommand("private", "Private"));
        await FluentActions.Awaiting(() => builder.Products.UpdateProductAsync(product.Id,
            new UpdateProductCommand(Name: "Changed", RelatedCollectionId: foreignId)))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => builder.Products.CreateProductAsync(new CreateProductCommand(
            "foreign", "Foreign", ProductKinds.Simple, CategoryId: foreignCategory.Id)))
            .Should().ThrowAsync<NotFoundException>();
        (await builder.Products.GetProductAsync(product.Id))!.Name.Should().Be("Dish");
    }

    [Fact]
    public async Task Browse_Should_SortResolvedNutritionBeforePagination_WithStaleAndMissingLast()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);
        await SeedDishAsync(ctx, tenantId, "alpha", 300, 10);
        await SeedDishAsync(ctx, tenantId, "beta", 400, 50);
        await SeedDishAsync(ctx, tenantId, "gamma", 400, 50);
        await SeedDishAsync(ctx, tenantId, "stale", 1, 999, stale: true);
        await service.CreateProductAsync(new CreateProductCommand("unknown", "unknown", ProductKinds.Simple));

        var protein = await service.ListProductsAsync(new ListProductsQuery(Sort: "protein-desc", PageSize: 1, Page: 2));
        protein.TotalCount.Should().Be(5);
        protein.Items.Should().ContainSingle().Which.Slug.Should().Be("gamma");
        var calories = await service.ListProductsAsync(new ListProductsQuery(Sort: "calories-asc"));
        calories.Items.Select(p => p.Slug).Should().Equal("alpha", "beta", "gamma", "stale", "unknown");
        var stale = calories.Items.Single(p => p.Slug == "stale");
        stale.Kcal.Should().BeNull();
        stale.ProteinGrams.Should().BeNull();
        stale.FibreGrams.Should().BeNull();
        stale.ContentIsStale.Should().BeTrue();
        stale.ServingLabel.Should().Be("Standard serving");
        stale.ContentVersion.Should().Be(1);
    }

    [Fact]
    public async Task TypedFacets_Should_UsePublishedNutritionAndExplicitFacts_NotLegacyAttributes()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);
        var facets = new FacetGroupService(ctx, new TestTenantProvider(tenantId));
        await facets.CreateAsync(new CreateFacetGroupCommand("calories", "Calories", FacetMatchKinds.Range,
            """[{"value":"under-500","label":"Under 500","max":500}]""", SourcePath: "nutrition.kcal"));
        await facets.CreateAsync(new CreateFacetGroupCommand("heat", "Heat", FacetMatchKinds.Attribute,
            """[{"value":"0","label":"None"},{"value":"3","label":"Hot"}]""", SourcePath: "heat"));
        await facets.CreateAsync(new CreateFacetGroupCommand("sugar", "Low sugar", FacetMatchKinds.Attribute,
            """[{"value":"true","label":"Yes"},{"value":"false","label":"No"}]""", SourcePath: "lowSugar"));
        var included = await SeedDishAsync(ctx, tenantId, "included", 499, 20);
        await SeedDishAsync(ctx, tenantId, "boundary", 500, 30);
        await SeedDishAsync(ctx, tenantId, "stale", 100, 99, stale: true);
        await service.CreateProductAsync(new CreateProductCommand("json-only", "JSON only", ProductKinds.Simple,
            AttributesJson: """{"nutrition":{"kcal":1},"heat":"0","lowSugar":"true"}"""));
        await service.UpdateProductAsync(included, new UpdateProductCommand(Heat: 0, LowSugar: false));

        var filtered = await service.ListProductsAsync(new ListProductsQuery(Facets:
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["calories"] = ["under-500"], ["heat"] = ["0"], ["sugar"] = ["false"],
            }));
        filtered.TotalCount.Should().Be(1);
        filtered.Items.Should().ContainSingle().Which.Slug.Should().Be("included");
        filtered.Items[0].Kcal.Should().Be(499);
        var under = await service.ListProductsAsync(new ListProductsQuery(Facets:
            new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["under-500"] }));
        under.Items.Select(p => p.Slug).Should().Equal("included");
    }

    [Fact]
    public async Task BrowseAndCollections_Should_UseVariantThatBecameDefault_AndSameMediaMetadata()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var optionBuilder = new OptionCatalogueBuilder(ctx, tenantId);
        await optionBuilder.BuildCatalogueAsync();
        var productId = await optionBuilder.BuildProductAsync();
        await optionBuilder.OfferAllAsync(productId);
        var content = CommerceTestHarness.NewContentService(ctx, tenantId);
        var defaults = (await content.GetAdminAsync(productId)).CurrentDefaultsSelectionJson;
        await content.UpsertContentAsync(productId, new UpsertProductContentCommand("Chicken", Kcal: 300, ProteinGrams: 10),
            new BlockWritePrecondition(defaults, null));
        await content.AddVariantAsync(productId, new UpsertContentVariantCommand(
            """{"protein":"salmon"}""", "Salmon serving", Kcal: 650, ProteinGrams: 40, FibreGrams: 7), defaults);
        await CommerceTestHarness.NewOptionService(ctx, tenantId)
            .SetRecommendedDefaultAsync(await optionBuilder.GroupIdAsync("protein"), "salmon");
        var builder = new MerchandisingBuilder(ctx, tenantId);
        var category = await builder.Products.CreateCategoryAsync(new CreateCategoryCommand("fish", "Fish"));
        await builder.Products.UpdateProductAsync(productId, new UpdateProductCommand(
            Description: "A salmon dish", CategoryId: category.Id, ComponentsLine: "with rice", IsPlaceholder: false));
        await builder.Products.ReplaceProductMediaAsync(productId, new ReplaceProductMediaCommand([
            new ProductMediaLine("https://example.test/menu.pdf", "doc", "Menu"),
            new ProductMediaLine("https://example.test/salmon.jpg", AltText: " Salmon with rice "),
        ]));
        await SeedDishAsync(ctx, tenantId, "other", 450, 25);
        await builder.WithCollectionAsync("featured", ("jollof", 1));

        var browse = await builder.Products.ListProductsAsync(new ListProductsQuery(Sort: "protein-desc", PageSize: 1));
        var card = browse.Items.Should().ContainSingle().Which;
        card.Id.Should().Be(productId);
        card.Kcal.Should().Be(650);
        card.ProteinGrams.Should().Be(40);
        card.FibreGrams.Should().Be(7);
        card.ServingLabel.Should().Be("Salmon serving");
        card.ContentIsStale.Should().BeFalse();
        card.Description.Should().Be("A salmon dish");
        card.CategoryName.Should().Be("Fish");
        card.CategorySlug.Should().Be("fish");
        card.IsPlaceholder.Should().BeFalse();
        card.HeroImageUrl.Should().Be("https://example.test/salmon.jpg");
        card.HeroImageAltText.Should().Be("Salmon with rice");
        var collection = (await builder.Collections.GetPublicBySlugAsync("featured"))!;
        collection.Products.Should().ContainSingle().Which.Should().BeEquivalentTo(card);
        (await content.ResolveAsync(productId, null))!.Nutrition.Kcal.Should().Be(card.Kcal);
    }

    [Fact]
    public async Task ReplaceMedia_Should_RoundTripAltAndRejectOversizeBeforeRemovingExistingRows()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewProductService(ctx, tenantId);
        var product = await service.CreateProductAsync(new CreateProductCommand("dish", "Dish", ProductKinds.Simple));
        var initial = await service.ReplaceProductMediaAsync(product.Id, new ReplaceProductMediaCommand([
            new ProductMediaLine("https://example.test/one.jpg", AltText: " One "),
            new ProductMediaLine("https://example.test/two.jpg", AltText: "Two"),
        ]));
        var reordered = await service.ReplaceProductMediaAsync(product.Id, new ReplaceProductMediaCommand(
            initial.Reverse().Select(m => new ProductMediaLine(m.Url, m.Kind, m.AltText)).ToList()));
        reordered.Select(m => m.AltText).Should().Equal("Two", "One");
        await FluentActions.Awaiting(() => service.ReplaceProductMediaAsync(product.Id,
            new ReplaceProductMediaCommand([new ProductMediaLine("https://example.test/bad.jpg", AltText: new string('x', 501))])))
            .Should().ThrowAsync<StorefrontValidationException>();
        await ctx.SaveChangesAsync();
        (await service.GetProductAsync(product.Id))!.Media.Select(m => m.AltText).Should().Equal("Two", "One");
    }

    private static async Task<Guid> SeedDishAsync(
        CommerceDbContext ctx, Guid tenantId, string slug, decimal kcal, decimal protein, bool stale = false)
    {
        var product = await CommerceTestHarness.NewProductService(ctx, tenantId)
            .CreateProductAsync(new CreateProductCommand(slug, slug, ProductKinds.Simple));
        ctx.ProductContents.Add(new ProductContent
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id,
            ServingLabel = "Standard serving", Kcal = kcal, ProteinGrams = protein, FibreGrams = 5,
            RequiresReview = stale, ContentVersion = 1,
        });
        await ctx.SaveChangesAsync();
        return product.Id;
    }
}
