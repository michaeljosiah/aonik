using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 070 §7/§10/§11 — the product PATCH, the admin detail split, media full-replace, and the
/// category lifecycle. Covers acceptance criteria A10, A11 (service half), A13 (write half), A17.
/// </summary>
public class CatalogMerchandisingProductTests
{
    // ─── Product PATCH ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProduct_Should_ApplyOnlySuppliedMembers()
    {
        // A10 — PATCH semantics: a tags/keywords edit must not disturb name, status or category.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();

        var updated = await builder.Products.UpdateProductAsync(ids["jollof"], new UpdateProductCommand(
            TagsJson: """["vegan","new"]""",
            SearchKeywordsJson: """["party","owambe"]"""));

        updated.Name.Should().Be("Jollof Rice");
        updated.Status.Should().Be(ProductStatuses.Active);
        updated.CategoryId.Should().Be(builder.RiceMainsId);
        updated.TagsJson.Should().Contain("new");
        updated.SearchKeywords.Should().BeEquivalentTo(["party", "owambe"]);
    }

    [Fact]
    public async Task UpdateProduct_Should_RejectMalformedJson_OnWrite()
    {
        // A13 (write half) — reads are defensive, writes are strict: a 400 at authoring beats a
        // warning-logged half-rendered row later.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var id = ids["jollof"];

        var badTags = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(TagsJson: "{oops"));
        var badAttributes = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(AttributesJson: """["array"]"""));
        var badKeywords = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(SearchKeywordsJson: """[1,2]"""));
        var hugeKeywords = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(
            SearchKeywordsJson: $"""["{new string('k', 1100)}"]"""));

        await badTags.Should().ThrowAsync<StorefrontValidationException>();
        await badAttributes.Should().ThrowAsync<StorefrontValidationException>();
        await badKeywords.Should().ThrowAsync<StorefrontValidationException>();
        await hugeKeywords.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task UpdateProduct_Should_RejectBlankJsonStrings_OnTheStrictWritePath()
    {
        // Reads treat blank as empty for LEGACY rows; a write submitting whitespace is a client
        // bug that would silently erase stored tags/keywords if accepted.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var id = ids["jollof"];

        var blankTags = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(TagsJson: "   "));
        var blankAttributes = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(AttributesJson: ""));

        await blankTags.Should().ThrowAsync<StorefrontValidationException>();
        await blankAttributes.Should().ThrowAsync<StorefrontValidationException>();

        // The stored values survived the rejected writes.
        (await builder.Products.GetAdminProductAsync(id))!.TagsJson.Should().Contain("vegan");
    }

    [Fact]
    public async Task CreateProduct_Should_RejectMalformedJson_Too()
    {
        // §11 extends the hygiene to the create path, which stored arbitrary strings until now.
        var (builder, _) = await ArrangeAsync();

        var act = () => builder.Products.CreateProductAsync(new CreateProductCommand(
            "broken", "Broken", ProductKinds.Simple, TagsJson: "not json"));

        await act.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task CreateProduct_Should_AcceptSearchKeywords_InOneRequest()
    {
        // Keywords are part of product authoring — a new product must be searchable without a
        // follow-up PATCH.
        var (builder, _) = await ArrangeAsync();

        var created = await builder.Products.CreateProductAsync(new CreateProductCommand(
            "moimoi", "Moi Moi", ProductKinds.Simple,
            SearchKeywordsJson: """["beans","steamed"]"""));

        (await builder.Products.GetAdminProductAsync(created.Id))!
            .SearchKeywords.Should().BeEquivalentTo(["beans", "steamed"]);

        var found = await builder.Products.ListProductsAsync(new ListProductsQuery(Search: "steamed"));
        found.Items.Should().ContainSingle(p => p.Slug == "moimoi");

        var badKeywords = () => builder.Products.CreateProductAsync(new CreateProductCommand(
            "broken-kw", "Broken", ProductKinds.Simple, SearchKeywordsJson: "{nope"));
        await badKeywords.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task UpdateProduct_Should_ValidateStatusCategoryAndClearCategory()
    {
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var id = ids["jollof"];

        var badStatus = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(Status: "Retired"));
        var badCategory = () => builder.Products.UpdateProductAsync(id, new UpdateProductCommand(CategoryId: Guid.NewGuid()));
        await badStatus.Should().ThrowAsync<StorefrontValidationException>();
        await badCategory.Should().ThrowAsync<NotFoundException>();

        var cleared = await builder.Products.UpdateProductAsync(id, new UpdateProductCommand(ClearCategory: true));
        cleared.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task GetAdminProduct_Should_CarryKeywords_ThatThePublicDtoCannot()
    {
        // A11 (service half) — the admin detail includes keywords; ProductDto has no member for
        // them, so the public read structurally cannot leak them. The endpoint test walks the
        // serialized JSON of both.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();

        var admin = await builder.Products.GetAdminProductAsync(ids["jollof"]);
        var publicDto = await builder.Products.GetProductAsync(ids["jollof"]);

        admin!.SearchKeywords.Should().BeEquivalentTo(["party", "naija"]);
        typeof(ProductDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(name => name.Contains("Keyword", StringComparison.OrdinalIgnoreCase));
        publicDto.Should().NotBeNull();
    }

    // ─── Media ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReplaceMedia_Should_ReorderAndRemove_AndFeedTheHeroImage()
    {
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var id = ids["jollof"];

        // Reverse the two seeded images: the hero (first by SortOrder) must follow.
        var media = await builder.Products.ReplaceProductMediaAsync(id, new ReplaceProductMediaCommand(
            [new ProductMediaLine("https://cdn.example/jollof-2.jpg"), new ProductMediaLine("https://cdn.example/jollof-hero.jpg")]));
        media.Select(m => m.SortOrder).Should().Equal(0, 1);

        var browse = await builder.Products.ListProductsAsync(new ListProductsQuery(Status: ProductStatuses.Active));
        browse.Items.Single(p => p.Slug == "jollof").HeroImageUrl.Should().Be("https://cdn.example/jollof-2.jpg");

        // Explicit empty clears; null is a malformed payload, not a clear.
        (await builder.Products.ReplaceProductMediaAsync(id, new ReplaceProductMediaCommand([]))).Should().BeEmpty();
        var nullItems = () => builder.Products.ReplaceProductMediaAsync(id, new ReplaceProductMediaCommand(null));
        await nullItems.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task ReplaceMedia_Should_RejectBadUrlsAndKinds()
    {
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();

        var blankUrl = () => builder.Products.ReplaceProductMediaAsync(ids["jollof"], new ReplaceProductMediaCommand(
            [new ProductMediaLine("  ")]));
        var badKind = () => builder.Products.ReplaceProductMediaAsync(ids["jollof"], new ReplaceProductMediaCommand(
            [new ProductMediaLine("https://cdn.example/x.mp4", "video")]));
        // 1024 is the mapped column bound — a wider service limit would pass validation and then
        // fail SaveChanges as a 500 on SQL Server.
        var tooLong = () => builder.Products.ReplaceProductMediaAsync(ids["jollof"], new ReplaceProductMediaCommand(
            [new ProductMediaLine($"https://cdn.example/{new string('x', 1500)}.jpg")]));

        await blankUrl.Should().ThrowAsync<StorefrontValidationException>();
        await badKind.Should().ThrowAsync<StorefrontValidationException>();
        await tooLong.Should().ThrowAsync<StorefrontValidationException>();
    }

    // ─── Categories ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CategoryTree_Should_ServeActiveNodesSorted_AndHideDeactivatedSubtrees()
    {
        // A17 — deactivating a node hides its whole subtree from the tree; reactivation restores
        // it with no other write.
        var (builder, _) = await ArrangeAsync();

        var tree = await builder.Products.GetCategoryTreeAsync();
        tree.Select(n => n.Slug).Should().Equal("mains", "soups");
        tree.Single(n => n.Slug == "mains").Children.Should().ContainSingle(c => c.Slug == "rice-mains");

        await builder.Products.UpdateCategoryAsync(builder.MainsId, new UpdateCategoryCommand("Mains", IsActive: false));
        var hidden = await builder.Products.GetCategoryTreeAsync();
        hidden.Select(n => n.Slug).Should().Equal("soups");

        await builder.Products.UpdateCategoryAsync(builder.MainsId, new UpdateCategoryCommand("Mains", IsActive: true));
        var restored = await builder.Products.GetCategoryTreeAsync();
        restored.Single(n => n.Slug == "mains").Children.Should().ContainSingle(c => c.Slug == "rice-mains");
    }

    [Fact]
    public async Task UpdateCategory_Should_RejectCycles_AndPreserveOmittedMembers()
    {
        var (builder, _) = await ArrangeAsync();

        // rice-mains is a child of mains; making mains a child of rice-mains would orbit forever.
        var cycle = () => builder.Products.UpdateCategoryAsync(builder.MainsId, new UpdateCategoryCommand(
            "Mains", ParentCategoryId: builder.RiceMainsId));
        var selfParent = () => builder.Products.UpdateCategoryAsync(builder.MainsId, new UpdateCategoryCommand(
            "Mains", ParentCategoryId: builder.MainsId));
        var unknownParent = () => builder.Products.UpdateCategoryAsync(builder.MainsId, new UpdateCategoryCommand(
            "Mains", ParentCategoryId: Guid.NewGuid()));

        await cycle.Should().ThrowAsync<StorefrontValidationException>();
        await selfParent.Should().ThrowAsync<StorefrontValidationException>();
        await unknownParent.Should().ThrowAsync<NotFoundException>();

        // A rename-only update leaves parentage and lifecycle untouched.
        var renamed = await builder.Products.UpdateCategoryAsync(builder.RiceMainsId, new UpdateCategoryCommand("Rice & Grains"));
        renamed.ParentCategoryId.Should().Be(builder.MainsId);

        // ClearParent promotes to root — distinguishable from "unchanged" by the explicit flag.
        var promoted = await builder.Products.UpdateCategoryAsync(builder.RiceMainsId, new UpdateCategoryCommand(
            "Rice & Grains", ClearParent: true));
        promoted.ParentCategoryId.Should().BeNull();
    }

    private static async Task<(MerchandisingBuilder Builder, Aonik.Commerce.Persistence.CommerceDbContext Ctx)> ArrangeAsync()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new MerchandisingBuilder(ctx, tenantId);
        await builder.WithCategoriesAsync();
        await builder.WithProductsAsync();
        return (builder, ctx);
    }
}
