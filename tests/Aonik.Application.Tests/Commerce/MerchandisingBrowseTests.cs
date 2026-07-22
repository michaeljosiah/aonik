using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 070 §6/§7 — the merchandised browse: facet matching (all four kinds, OR-within,
/// AND-across), hidden-keyword search, collection filtering and sort precedence.
/// Covers acceptance criteria A2, A3, A5 (service half), A6, A13, A15, A16, A17.
/// </summary>
public class MerchandisingBrowseTests
{
    [Fact]
    public async Task ListProducts_Should_OrWithinAGroup_When_TwoValuesOfOneFacetSelected()
    {
        // A2 — Vegan + Vegetarian selected in one group → either matches.
        var (browse, _, _) = await ArrangeAsync();

        var result = await browse(new Dictionary<string, IReadOnlyList<string>> { ["dietary"] = ["vegan", "vegetarian"] }, null, null);

        result.Items.Select(p => p.Slug).Should().BeEquivalentTo(["jollof", "garden-salad"]);
    }

    [Fact]
    public async Task ListProducts_Should_AndAcrossGroups_When_TwoFacetsSelected()
    {
        // A2 — vegan AND hot spice → nothing (jollof is medium); vegan AND medium → jollof only.
        var (browse, _, _) = await ArrangeAsync();

        var none = await browse(new Dictionary<string, IReadOnlyList<string>>
        {
            ["dietary"] = ["vegan"],
            ["spice"] = ["hot"],
        }, null, null);
        var one = await browse(new Dictionary<string, IReadOnlyList<string>>
        {
            ["dietary"] = ["vegan"],
            ["spice"] = ["medium"],
        }, null, null);

        none.Items.Should().BeEmpty();
        one.Items.Should().ContainSingle().Which.Slug.Should().Be("jollof");
    }

    [Fact]
    public async Task ListProducts_Should_MatchRangeBandsHalfOpen_IncludingTheBoundary()
    {
        // A3 — [null,500): 450 matches, 500-800's exclusive max means 800 kcal matches NEITHER
        // band, and a product with no kcal value matches no band at all.
        var (browse, _, _) = await ArrangeAsync();

        var under = await browse(new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["under-500"] }, null, null);
        var mid = await browse(new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["500-800"] }, null, null);
        var both = await browse(new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["under-500", "500-800"] }, null, null);

        under.Items.Select(p => p.Slug).Should().BeEquivalentTo(["jollof", "garden-salad"]);
        mid.Items.Select(p => p.Slug).Should().BeEquivalentTo(["egusi"], "800 kcal is outside the half-open [500,800) band");
        both.Items.Select(p => p.Slug).Should().NotContain("pounded-yam", "a value on no band matches nothing");
    }

    [Fact]
    public async Task ListProducts_Should_MatchDescendantCategories_When_AParentIsSelected()
    {
        // §6 — selecting "mains" matches jollof, which sits in the CHILD category rice-mains.
        var (browse, _, _) = await ArrangeAsync();

        var result = await browse(new Dictionary<string, IReadOnlyList<string>> { ["category"] = ["mains"] }, null, null);

        result.Items.Select(p => p.Slug).Should().BeEquivalentTo(["jollof", "garden-salad", "pounded-yam"]);
    }

    [Fact]
    public async Task ListProducts_Should_StopMatchingACategory_When_ItIsDeactivated()
    {
        // A17 — deactivating rice-mains removes jollof from the mains closure with no other write.
        var (browse, builder, _) = await ArrangeAsync();
        await builder.Products.UpdateCategoryAsync(builder.RiceMainsId, new UpdateCategoryCommand("Rice dishes", IsActive: false));

        var result = await browse(new Dictionary<string, IReadOnlyList<string>> { ["category"] = ["mains"] }, null, null);

        result.Items.Select(p => p.Slug).Should().BeEquivalentTo(["garden-salad", "pounded-yam"]);
    }

    [Fact]
    public async Task ListProducts_Should_NotMatchAnActiveChild_UnderADeactivatedAncestor()
    {
        // A17's sharper edge: rice-mains itself stays IsActive, but its PARENT mains is retired.
        // The public tree hides the child (unreachable), so a stale deep link submitting the
        // child's own token must not expose the hidden subtree's products through the facet.
        var (browse, builder, _) = await ArrangeAsync();
        var facets = builder.Facets;
        var category = (await facets.ListAdminAsync()).Single(g => g.Key == "category");
        await facets.UpdateAsync(category.Id, new UpdateFacetGroupCommand(
            "Category",
            OptionsJson: """[{"value":"mains","label":"Mains"},{"value":"rice-mains","label":"Rice dishes"},{"value":"soups","label":"Soups"}]"""));

        await builder.Products.UpdateCategoryAsync(builder.MainsId, new UpdateCategoryCommand("Mains", IsActive: false));

        var viaChildToken = await browse(new Dictionary<string, IReadOnlyList<string>> { ["category"] = ["rice-mains"] }, null, null);

        viaChildToken.Items.Should().BeEmpty("an active child under an inactive ancestor is hidden from the public tree, and the facet must agree");
    }

    [Fact]
    public async Task ListProducts_Should_NotThrow_When_AnAttributeNumberOverflowsDecimal()
    {
        // 1e100 is valid JSON that decimal cannot represent. The defensive-read guarantee means
        // it matches no band and compares as raw text — never a 500 on an anonymous browse.
        var (browse, builder, ctx) = await ArrangeWithContextAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var yam = await ctx.Products.FindAsync(ids["pounded-yam"]);
        yam!.AttributesJson = """{"nutrition":{"kcal":1e100},"spice":1e100}""";
        await ctx.SaveChangesAsync();

        var byRange = await browse(new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["under-500", "500-800"] }, null, null);
        var bySpice = await browse(new Dictionary<string, IReadOnlyList<string>> { ["spice"] = ["hot"] }, null, null);

        byRange.Items.Select(p => p.Slug).Should().NotContain("pounded-yam");
        bySpice.Items.Select(p => p.Slug).Should().NotContain("pounded-yam");
    }

    [Fact]
    public async Task ListProducts_Should_Reject_UnknownFacetKeysAndValues()
    {
        // §6 — a storefront bug should be loud: unknown keys/values are 400s, never ignored.
        var (browse, _, _) = await ArrangeAsync();

        var unknownKey = () => browse(new Dictionary<string, IReadOnlyList<string>> { ["made-up"] = ["x"] }, null, null);
        var unknownValue = () => browse(new Dictionary<string, IReadOnlyList<string>> { ["dietary"] = ["carnivore"] }, null, null);

        await unknownKey.Should().ThrowAsync<StorefrontValidationException>();
        await unknownValue.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task ListProducts_Should_RejectALabel_Where_AValueTokenBelongs()
    {
        // A15 — "Under 500 kcal" is the label; the request token is "under-500". Labels are free
        // to change precisely because they are never valid request values.
        var (browse, _, _) = await ArrangeAsync();

        var act = () => browse(new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["Under 500 kcal"] }, null, null);

        (await act.Should().ThrowAsync<StorefrontValidationException>())
            .Which.Message.Should().Contain("values, not labels");
    }

    [Fact]
    public async Task ListProducts_Should_KeepWorking_When_AFacetLabelIsRenamed()
    {
        // A15 — renaming the display label must not break the stored value token.
        var (browse, builder, _) = await ArrangeAsync();
        var calories = (await builder.Facets.ListAdminAsync()).Single(g => g.Key == "calories");
        await builder.Facets.UpdateAsync(calories.Id, new UpdateFacetGroupCommand(
            "Calories",
            OptionsJson: """[{"value":"under-500","label":"Light","min":null,"max":500},{"value":"500-800","label":"Hearty","min":500,"max":800}]"""));

        var result = await browse(new Dictionary<string, IReadOnlyList<string>> { ["calories"] = ["under-500"] }, null, null);

        result.Items.Select(p => p.Slug).Should().BeEquivalentTo(["jollof", "garden-salad"]);
    }

    [Fact]
    public async Task ListProducts_Should_MatchHiddenKeywords_CaseInsensitively_AndDescriptions()
    {
        // A5 (service half) — "party" appears only in jollof's hidden keywords... and also its
        // description here, so use "naija" for the keyword-only assertion. Case must not matter.
        var (browse, _, search) = await ArrangeAsync();

        (await search("NAIJA")).Items.Should().ContainSingle().Which.Slug.Should().Be("jollof");
        (await search("shaki")).Items.Should().ContainSingle().Which.Slug.Should().Be("egusi");
        (await search("smoky")).Items.Should().ContainSingle("description matches too").Which.Slug.Should().Be("jollof");
        (await search("wagyu")).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListProducts_Should_NeverSerializeKeywords_IntoTheSummaryRow()
    {
        // A5/A11 (type half) — the summary row simply has no keywords member; the endpoint-level
        // test walks the serialized JSON. Belt and braces on purpose.
        typeof(ProductSummaryDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(name => name.Contains("Keyword", StringComparison.OrdinalIgnoreCase));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ListProducts_Should_EnrichTheRow_WithHeroImageTagsAndSurcharge()
    {
        // A6 — the grid card renders from the row alone: first image by SortOrder, parsed tags,
        // attributes pass-through. No price field exists on the type to assert absent.
        var (browse, builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        await builder.Products.UpdateProductAsync(ids["jollof"], new UpdateProductCommand()); // no-op PATCH must not disturb enrichment

        var result = await browse(null, null, null);
        var jollof = result.Items.Single(p => p.Slug == "jollof");

        jollof.HeroImageUrl.Should().Be("https://cdn.example/jollof-hero.jpg");
        jollof.Tags.Should().BeEquivalentTo(["vegan"]);
        jollof.AttributesJson.Should().Contain("kcal");
    }

    [Fact]
    public async Task ListProducts_Should_SurviveAMalformedLegacyRow_AndExcludeItFromFacets()
    {
        // A13 — one bad row must never 500 the public browse: it renders with empty tags and
        // drops out of facet matching, and everything else keeps working.
        var (browse, builder, ctx) = await ArrangeWithContextAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var salad = await ctx.Products.FindAsync(ids["garden-salad"]);
        salad!.TagsJson = "{not json";
        salad.AttributesJson = "[also wrong]";
        await ctx.SaveChangesAsync();

        var all = await browse(null, null, null);
        var vegan = await browse(new Dictionary<string, IReadOnlyList<string>> { ["dietary"] = ["vegan"] }, null, null);

        all.Items.Single(p => p.Slug == "garden-salad").Tags.Should().BeEmpty();
        vegan.Items.Select(p => p.Slug).Should().BeEquivalentTo(["jollof"], "the malformed row cannot facet-match");
    }

    [Fact]
    public async Task ListProducts_Should_DefaultToRankOrder_WithinACollection_AndLetSortOverride()
    {
        // A16 — collection without sort = curated rank; explicit sort=name overrides;
        // sort=rank without a collection is meaningless and loud.
        var (browse, builder, _) = await ArrangeAsync();
        await builder.WithCollectionAsync("featured", ("pounded-yam", 1), ("jollof", 2), ("egusi", 3));

        var ranked = await browse(null, "featured", null);
        var byName = await browse(null, "featured", "name");
        var rankWithout = () => browse(null, null, "rank");

        ranked.Items.Select(p => p.Slug).Should().ContainInOrder("pounded-yam", "jollof", "egusi");
        byName.Items.Select(p => p.Slug).Should().ContainInOrder("egusi", "jollof", "pounded-yam");
        await rankWithout.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task ListProducts_Should_Reject_UnknownSortAndUnknownCollection()
    {
        var (browse, _, _) = await ArrangeAsync();

        var badSort = () => browse(null, null, "sideways");
        var badCollection = () => browse(null, "no-such-rail", null);

        await badSort.Should().ThrowAsync<StorefrontValidationException>();
        await badCollection.Should().ThrowAsync<StorefrontValidationException>();
    }

    // ─── Plumbing ────────────────────────────────────────────────────────────

    private delegate Task<PagedResult<ProductSummaryDto>> Browse(
        Dictionary<string, IReadOnlyList<string>>? facets, string? collection, string? sort);

    private static async Task<(Browse Browse, MerchandisingBuilder Builder, Func<string, Task<PagedResult<ProductSummaryDto>>> Search)> ArrangeAsync()
    {
        var (browse, builder, _) = await ArrangeWithContextAsync();
        return (browse, builder, term => builder.Products.ListProductsAsync(
            new ListProductsQuery(Status: ProductStatuses.Active, Search: term)));
    }

    private static async Task<(Browse Browse, MerchandisingBuilder Builder, Aonik.Commerce.Persistence.CommerceDbContext Ctx)> ArrangeWithContextAsync()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new MerchandisingBuilder(ctx, tenantId);
        await builder.WithCategoriesAsync();
        await builder.WithFacetsAsync();
        await builder.WithProductsAsync();

        Browse browse = (facets, collection, sort) => builder.Products.ListProductsAsync(
            new ListProductsQuery(Status: ProductStatuses.Active, Facets: facets, Collection: collection, Sort: sort));

        return (browse, builder, ctx);
    }
}
