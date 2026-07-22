using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 067 §9 — the authoring invariants V-C1…V-C8, the ContentVersion lifecycle, the §6
/// default-change hooks, and coverage. Covers A7, A8, A10, A12–A14, A16, A18.
/// </summary>
public class ProductContentAuthoringTests
{
    // ─── V-C1 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddVariant_Should_RejectSelections_TheProductDoesNotOffer()
    {
        // A7 — 066's validation runs first: a choice outside the narrowing is its V2, surfaced
        // through the same exception contract customer input gets.
        var (content, productId, _, _) = await ArrangeAsync();

        var act = () => content.AddVariantAsync(productId, Variant("""{"protein":"wagyu"}""", 640));

        await act.Should().ThrowAsync<OptionValidationException>();
    }

    [Fact]
    public async Task AddVariant_Should_RejectTheAllDefaultsSelection()
    {
        // A16 — that content belongs on the default block; authoring it as a variant would
        // shadow the block's review lifecycle. Redundant default-valued entries normalise away,
        // so {"protein":"chicken"} IS the all-defaults selection.
        var (content, productId, _, _) = await ArrangeAsync();

        var explicitDefaults = () => content.AddVariantAsync(productId, Variant("""{"protein":"chicken"}""", 640));
        var empty = () => content.AddVariantAsync(productId, Variant("{}", 640));

        (await explicitDefaults.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C1");
        (await empty.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C1");
    }

    [Fact]
    public async Task AddVariant_Should_StoreTheCompleteCanonicalSelection_FromPartialInput()
    {
        // §4 — authoring input may be partial; what is stored is complete, so identity survives
        // later default moves.
        var (content, productId, _, _) = await ArrangeAsync();

        var variant = await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));

        variant.SelectionJson.Should().Contain("\"portion\"").And.Contain("\"side\"").And.Contain("\"heat\"");
        variant.SelectionJson.Should().Contain("\"protein\":\"salmon\"");
    }

    // ─── V-C2 / V-C6 — the mixed-panel guards ────────────────────────────────

    [Fact]
    public async Task AddVariant_Should_RejectAFigureSet_MissingWhatTheDefaultPublishes()
    {
        // A8 — a panel mixing default kcal with variant protein would be a derived panel by the
        // back door. More is allowed; fewer is not.
        var (content, productId, _, _) = await ArrangeAsync();

        var act = () => content.AddVariantAsync(productId, new UpsertContentVariantCommand(
            """{"protein":"salmon"}""", "Salmon", Kcal: 640 /* missing protein/carbs/fat */));

        (await act.Should().ThrowAsync<StorefrontValidationException>())
            .Which.Message.Should().Contain("V-C2").And.Contain("proteinGrams");
    }

    [Fact]
    public async Task AddVariant_Should_AllowOverPublishing()
    {
        var (content, productId, _, _) = await ArrangeAsync();

        var variant = await content.AddVariantAsync(productId, new UpsertContentVariantCommand(
            """{"protein":"salmon"}""", "Salmon",
            Kcal: 640, ProteinGrams: 30, CarbsGrams: 60, FatGrams: 15,
            SugarsGrams: 4 /* the default publishes no sugars — over-publishing is fine */));

        variant.Nutrition.SugarsGrams.Should().Be(4);
    }

    [Fact]
    public async Task UpsertContent_Should_RejectAddingAFigure_ActiveVariantsDoNotPublish()
    {
        // A14 — V-C6 names the offenders; the fix is updating them first (or the same batch).
        // Removing a figure the variants still publish succeeds: they may over-publish.
        var (content, productId, _, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));

        var addSugars = () => content.UpsertContentAsync(productId, DefaultBlock() with { SugarsGrams = 6 });
        (await addSugars.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C6");

        var removeFat = await content.UpsertContentAsync(productId, DefaultBlock() with { FatGrams = null });
        removeFat.Nutrition.FatGrams.Should().BeNull();
    }

    // ─── V-C3 / V-C7 / V-C8 ──────────────────────────────────────────────────

    [Fact]
    public async Task Authoring_Should_RequireServingLabels_AndSaneFigures()
    {
        var (content, productId, _, _) = await ArrangeAsync();

        var noLabel = () => content.UpsertContentAsync(productId, DefaultBlock() with { ServingLabel = " " });
        var negative = () => content.UpsertContentAsync(productId, DefaultBlock() with { Kcal = -500 });
        var overflow = () => content.UpsertContentAsync(productId, DefaultBlock() with { Kcal = 12_345_678m });

        (await noLabel.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C3");
        // A13 — SQL would happily store −500 kcal; the service must not.
        (await negative.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C7");
        (await overflow.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C7");
    }

    [Fact]
    public async Task AddVariant_Should_RequireADefaultBlockFirst()
    {
        // A12 — the block is the baseline V-C2 is defined against.
        var (content, _, builder, _) = await ArrangeAsync();
        var bareId = await builder.BuildProductAsync("bare");
        await builder.OfferAsync(bareId, new ProductOptionGroupLine("protein"));

        var act = () => content.AddVariantAsync(bareId, Variant("""{"protein":"salmon"}""", 640));

        (await act.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C8");
    }

    [Fact]
    public async Task AddVariant_Should_RejectADuplicateActiveCombination()
    {
        var (content, productId, _, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));

        var duplicate = () => content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 700));

        (await duplicate.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C4");
    }

    // ─── ContentVersion lifecycle ────────────────────────────────────────────

    [Fact]
    public async Task ContentVersion_Should_BumpOnEveryWriteKind()
    {
        // §8 — the version is the cache key: EVERY write that changes what resolution returns
        // must move it, review-flagging included (A18's InMemory-observable half).
        var (content, productId, builder, ctx) = await ArrangeAsync();

        var v0 = (await content.ResolveAsync(productId, null))!.ContentVersion;

        var variant = await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));
        var v1 = (await content.ResolveAsync(productId, null))!.ContentVersion;
        v1.Should().BeGreaterThan(v0);

        await content.UpdateVariantAsync(variant.Id, Variant("""{"protein":"salmon"}""", 655));
        var v2 = (await content.ResolveAsync(productId, null))!.ContentVersion;
        v2.Should().BeGreaterThan(v1);

        await content.DeactivateVariantAsync(variant.Id);
        var v3 = (await content.ResolveAsync(productId, null))!.ContentVersion;
        v3.Should().BeGreaterThan(v2);

        await content.ConfirmContentReviewAsync(productId);
        var v4 = (await content.ResolveAsync(productId, null))!.ContentVersion;
        v4.Should().BeGreaterThan(v3);

        await content.UpsertContentAsync(productId, DefaultBlock());
        var v5 = (await content.ResolveAsync(productId, null))!.ContentVersion;
        v5.Should().BeGreaterThan(v4);
    }

    [Fact]
    public async Task ADefaultMove_Should_FlagTheBlock_AndBumpTheVersion_InTheSameWrite()
    {
        // A18 — the §6 reaction IS a content write: the flag changes what resolution returns, so
        // the version must move with it (staged into the SAME SaveChanges as the move).
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var before = (await content.ResolveAsync(productId, null))!.ContentVersion;

        var optionService = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        await optionService.SetRecommendedDefaultAsync(await builder.GroupIdAsync("protein"), "salmon");

        var block = ctx.ProductContents.Single(c => c.ProductId == productId);
        block.RequiresReview.Should().BeTrue();
        block.ContentVersion.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task ANarrowingWrite_Should_FlagTheBlock()
    {
        // §6 — a narrowing change can shift the effective default combination; the block is
        // flagged in the same SaveChanges as the narrowing.
        var (content, productId, builder, ctx) = await ArrangeAsync();

        await builder.OfferAsync(productId,
            new ProductOptionGroupLine("protein", AllowedChoiceKeys: ["salmon", "prawns"], DefaultChoiceKey: "salmon"));

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview.Should().BeTrue();
    }

    // ─── Coverage ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Coverage_Should_ListSingleChoiceGaps_AndDropAuthoredOnes()
    {
        // A10 — gaps are the single-choice deviations from the standard preparation, bounded by
        // Σ|offered choices|; authoring one removes it from the gap list.
        var (content, productId, _, _) = await ArrangeAsync();

        var before = await content.GetCoverageAsync(productId);
        before.SingleChoiceGaps.Should().Contain(g => g.GroupKey == "protein" && g.ChoiceKey == "salmon");
        before.SingleChoiceGaps.Should().Contain(g => g.GroupKey == "portion" && g.ChoiceKey == "full");
        before.SingleChoiceGaps.Should().NotContain(g => g.ChoiceKey == "chicken", "defaults are not deviations");

        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));
        var after = await content.GetCoverageAsync(productId);

        after.SingleChoiceGaps.Should().NotContain(g => g.GroupKey == "protein" && g.ChoiceKey == "salmon");
        after.Authored.Should().ContainSingle(a => a.IsActive);
    }

    // ─── Fixture ─────────────────────────────────────────────────────────────

    private static async Task<(ProductContentService Content, Guid ProductId, OptionCatalogueBuilder Builder, Aonik.Commerce.Persistence.CommerceDbContext Ctx)> ArrangeAsync()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        await builder.OfferAllAsync(productId);

        var content = CommerceTestHarness.NewContentService(ctx, tenantId);
        await content.UpsertContentAsync(productId, DefaultBlock());

        return (content, productId, builder, ctx);
    }

    private static Guid TenantOf(Aonik.Commerce.Persistence.CommerceDbContext ctx, Guid productId)
        => ctx.Products.Single(p => p.Id == productId).TenantId;

    private static UpsertProductContentCommand DefaultBlock() => new(
        "Light table 225g",
        Kcal: 450, ProteinGrams: 22, CarbsGrams: 60, FatGrams: 12,
        Ingredients: "Rice, tomato, prawn stock",
        Allergens: "Crustaceans");

    private static UpsertContentVariantCommand Variant(string selectionJson, decimal kcal) => new(
        selectionJson, "Variant serving",
        Kcal: kcal, ProteinGrams: 30, CarbsGrams: 60, FatGrams: 15);
}
