using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;

using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Builds the Spec 070 launch fixture — categories, facet groups, tagged/attributed products and a
/// curated collection — so the merchandising tests read as behaviour rather than setup. Mirrors
/// the client's menu: a category tree (Mains → Rice dishes, Soups), facets for dietary (Tag),
/// spice (Attribute), calories (Range) and category, and products spanning them.
/// </summary>
internal sealed class MerchandisingBuilder
{
    private readonly CommerceDbContext _ctx;
    private readonly Guid _tenantId;

    public MerchandisingBuilder(CommerceDbContext ctx, Guid tenantId)
    {
        _ctx = ctx;
        _tenantId = tenantId;
        Products = CommerceTestHarness.NewProductService(ctx, tenantId);
        Collections = new CollectionService(ctx, new Aonik.TestSupport.Multitenancy.TestTenantProvider(tenantId), NullLogger<CollectionService>.Instance, new FakeExtrasCatalog());
        Facets = new FacetGroupService(ctx, new Aonik.TestSupport.Multitenancy.TestTenantProvider(tenantId));
    }

    public ProductService Products { get; }
    public CollectionService Collections { get; }
    public FacetGroupService Facets { get; }

    public Guid MainsId { get; private set; }
    public Guid RiceMainsId { get; private set; }
    public Guid SoupsId { get; private set; }

    public async Task<MerchandisingBuilder> WithCategoriesAsync()
    {
        MainsId = (await Products.CreateCategoryAsync(new CreateCategoryCommand("mains", "Mains", SortOrder: 1))).Id;
        RiceMainsId = (await Products.CreateCategoryAsync(new CreateCategoryCommand("rice-mains", "Rice dishes", MainsId, SortOrder: 1))).Id;
        SoupsId = (await Products.CreateCategoryAsync(new CreateCategoryCommand("soups", "Soups & Stews", SortOrder: 2))).Id;
        return this;
    }

    public async Task<MerchandisingBuilder> WithFacetsAsync()
    {
        await Facets.CreateAsync(new CreateFacetGroupCommand(
            "dietary", "Dietary", FacetMatchKinds.Tag,
            """[{"value":"vegan","label":"Vegan"},{"value":"vegetarian","label":"Vegetarian"},{"value":"gluten-free","label":"Gluten-free"}]""",
            SortOrder: 1));

        await Facets.CreateAsync(new CreateFacetGroupCommand(
            "spice", "Spice level", FacetMatchKinds.Attribute,
            """[{"value":"mild","label":"Mild"},{"value":"medium","label":"Medium"},{"value":"hot","label":"Hot"}]""",
            SourcePath: "spice", SortOrder: 2));

        await Facets.CreateAsync(new CreateFacetGroupCommand(
            "calories", "Calories", FacetMatchKinds.Range,
            """[{"value":"under-500","label":"Under 500 kcal","min":null,"max":500},{"value":"500-800","label":"500-800 kcal","min":500,"max":800}]""",
            SourcePath: "nutrition.kcal", SortOrder: 3));

        await Facets.CreateAsync(new CreateFacetGroupCommand(
            "category", "Category", FacetMatchKinds.Category,
            """[{"value":"mains","label":"Mains"},{"value":"soups","label":"Soups & Stews"}]""",
            SortOrder: 4));

        return this;
    }

    /// <summary>Four Active products spanning the facets, plus one Draft:
    /// jollof (rice-mains child category, vegan, medium spice, 450 kcal, keyword "party", media),
    /// egusi (soups, hot, 650 kcal, keyword "shaki"),
    /// salad (mains, vegan+gluten-free, mild, 320 kcal),
    /// pounded-yam (mains, 800 kcal — ON the 500-800 boundary's exclusive end),
    /// secret-dish (Draft, staged for collections).</summary>
    public async Task<MerchandisingBuilder> WithProductsAsync()
    {
        var jollof = await Products.CreateProductAsync(new CreateProductCommand(
            "jollof", "Jollof Rice", ProductKinds.Simple,
            Description: "Smoky party rice",
            CategoryId: RiceMainsId,
            TagsJson: """["vegan"]""",
            AttributesJson: """{"spice":"medium","nutrition":{"kcal":450}}"""));
        await Products.UpdateProductAsync(jollof.Id, new UpdateProductCommand(
            SearchKeywordsJson: """["party","naija"]"""));
        await Products.ReplaceProductMediaAsync(jollof.Id, new ReplaceProductMediaCommand(
            [new ProductMediaLine("https://cdn.example/jollof-hero.jpg"), new ProductMediaLine("https://cdn.example/jollof-2.jpg")]));

        var egusi = await Products.CreateProductAsync(new CreateProductCommand(
            "egusi", "Egusi Soup", ProductKinds.Simple,
            CategoryId: SoupsId,
            AttributesJson: """{"spice":"hot","nutrition":{"kcal":650}}"""));
        await Products.UpdateProductAsync(egusi.Id, new UpdateProductCommand(
            SearchKeywordsJson: """["shaki"]"""));

        await Products.CreateProductAsync(new CreateProductCommand(
            "garden-salad", "Garden Salad", ProductKinds.Simple,
            CategoryId: MainsId,
            TagsJson: """["vegan","gluten-free"]""",
            AttributesJson: """{"spice":"mild","nutrition":{"kcal":320}}"""));

        await Products.CreateProductAsync(new CreateProductCommand(
            "pounded-yam", "Pounded Yam", ProductKinds.Simple,
            CategoryId: MainsId,
            AttributesJson: """{"nutrition":{"kcal":800}}"""));

        await Products.CreateProductAsync(new CreateProductCommand(
            "secret-dish", "Secret Dish", ProductKinds.Simple, Status: ProductStatuses.Draft));

        return this;
    }

    public async Task<Guid> WithCollectionAsync(string slug = "featured", params (string Slug, int Rank)[] members)
    {
        var collection = await Collections.CreateAsync(new CreateCollectionCommand(slug, "Featured", Kind: CollectionKinds.Featured));

        if (members.Length > 0)
        {
            var bySlug = await ProductIdsBySlugAsync();
            await Collections.ReplaceItemsAsync(collection.Id, new ReplaceCollectionItemsCommand(
                members.Select(m => new CollectionItemLine(bySlug[m.Slug], m.Rank)).ToList()));
        }

        return collection.Id;
    }

    public async Task<Dictionary<string, Guid>> ProductIdsBySlugAsync()
    {
        var list = await Products.ListProductsAsync(new ListProductsQuery(PageSize: 200));
        return list.Items.ToDictionary(p => p.Slug, p => p.Id, StringComparer.Ordinal);
    }
}
