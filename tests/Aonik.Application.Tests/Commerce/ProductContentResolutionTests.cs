using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 067 §5/§6 — exact-selection resolution and the withheld-declaration fallback: authored,
/// or absent — never derived, never substituted. Covers A1–A6, A9, A11, A19, A20.
/// </summary>
public class ProductContentResolutionTests
{
    [Fact]
    public async Task Resolve_Should_ReturnNull_When_NoDefaultBlockExists()
    {
        // A12 (service half) — content is optional per product; absence is a defined state.
        var (content, _, builder, _) = await ArrangeAsync();
        var bareProductId = await builder.BuildProductAsync("bare");

        (await content.ResolveAsync(bareProductId, null)).Should().BeNull();
    }

    [Fact]
    public async Task Resolve_Should_ServeTheAuthoredVariant_ForItsExactCombination()
    {
        // A1 — the authored figures return with their own serving label: never default × 2.
        var (content, productId, _, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 640, label: "Full salmon 400g",
            ingredients: "Rice, salmon", allergens: "Fish"));

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));

        resolved!.ServingLabel.Should().Be("Full salmon 400g");
        resolved.Nutrition.Kcal.Should().Be(640);
        resolved.IsStandardPreparation.Should().BeFalse();
        resolved.DeclarationsWithheld.Should().BeFalse();
        resolved.Allergens.Should().Be("Fish");
        resolved.MatchedVariantSelectionJson.Should().NotBeNull();
    }

    [Fact]
    public async Task Resolve_Should_FallBackWithWithheldDeclarations_When_OnlyPartialVariantsExist()
    {
        // A2 — full-portion and salmon variants exist separately; full+salmon has NO exact match:
        // default figures with the standard-preparation caption, declarations and heating
        // withheld. Neither partial variant is served — a "closest" pick would present a
        // half-truth as fact.
        var (content, productId, builder, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"portion":"full"}""", kcal: 900, label: "Full table"));
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 640, label: "Salmon"));

        var resolved = await content.ResolveAsync(productId, Selection("""{"portion":"full","protein":"salmon"}"""));

        resolved!.IsStandardPreparation.Should().BeTrue();
        resolved.Nutrition.Kcal.Should().Be(450, "the DEFAULT block's figure, captioned — never a partial variant's");
        resolved.DeclarationsWithheld.Should().BeTrue();
        resolved.Ingredients.Should().BeNull();
        resolved.Allergens.Should().BeNull();
        resolved.HeatingWithheld.Should().BeTrue();
        resolved.Heating.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_Should_NeverLeakTheDefaultDeclaration_UnderAVariantThatReplacesIt()
    {
        // A3 — the customer who chose salmon must never read the standard preparation's
        // shellfish declaration under their selection, footnote or not.
        var (content, productId, _, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 640, label: "Salmon",
            ingredients: "Rice, salmon", allergens: "Fish"));

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));

        resolved!.Allergens.Should().Be("Fish");
        resolved.Allergens.Should().NotContain("Crustaceans");
        resolved.Ingredients.Should().NotContain("prawn");
    }

    [Fact]
    public async Task Resolve_Should_ServeTheBlockWithDeclarations_ForTheStandardPreparation()
    {
        // A4 — the standard preparation is exactly what the block describes: declarations and
        // heating serve, no caption, no staleness.
        var (content, productId, _, _) = await ArrangeAsync();

        var resolved = await content.ResolveAsync(productId, null);

        resolved!.IsStandardPreparation.Should().BeFalse();
        resolved.IsStale.Should().BeFalse();
        resolved.Ingredients.Should().Contain("prawn");
        resolved.Allergens.Should().Be("Crustaceans");
        resolved.DeclarationsWithheld.Should().BeFalse();
        resolved.HeatingWithheld.Should().BeFalse();
        resolved.Heating.Should().ContainSingle(h => h.Method == "Oven");
    }

    [Fact]
    public async Task Resolve_Should_TreatEqualPricedSwaps_AsNonDefault()
    {
        // A5 — chicken → prawns, both £0: still a different preparation. Price-neutral is never
        // content-neutral; the diff is by KEY via the canonical form.
        var (content, productId, _, _) = await ArrangeAsync();

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":"prawns"}"""));

        resolved!.IsStandardPreparation.Should().BeTrue();
        resolved.DeclarationsWithheld.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_Should_MatchMultiSelectCombinations_RegardlessOfOrder()
    {
        // A6 — the canonical form sorts multi-select arrays, so authoring and selection order
        // are irrelevant to identity.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", SelectionModeOverride: OptionSelectionModes.Multi));
        await content.AddVariantAsync(productId, Variant("""{"protein":["salmon","chicken"]}""", kcal: 800, label: "Both"));

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":["chicken","salmon"]}"""));

        resolved!.ServingLabel.Should().Be("Both");
        resolved.Nutrition.Kcal.Should().Be(800);
    }

    [Fact]
    public async Task Resolve_Should_WithholdVariantDeclarations_When_TheyWereNotAuthored()
    {
        // A19 — explicit-or-withheld, never inherited: a variant with figures but null
        // declarations withholds them even though the default block has declarations, and a
        // later default-block edit changes nothing about what the variant serves.
        var (content, productId, _, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 640, label: "Salmon"));

        var before = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        before!.DeclarationsWithheld.Should().BeTrue();
        before.Allergens.Should().BeNull("inheritance from the block is exactly the §2 incident");

        await content.UpsertContentAsync(productId, DefaultBlock(allergens: "Crustaceans, Celery"));
        var after = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        after!.Allergens.Should().BeNull("the block edit must not alter what the variant serves");
    }

    [Fact]
    public async Task Resolve_Should_ServeVariantHeating_AndWithholdItOnFallback()
    {
        // A20 — heating is option-dependent content: substituted timings are unsafe food.
        var (content, productId, _, _) = await ArrangeAsync();
        await content.AddVariantAsync(productId, Variant("""{"portion":"full"}""", kcal: 900, label: "Full",
            heatingJson: """[{"method":"Oven","body":"35 min at 180C"}]"""));

        var variantHit = await content.ResolveAsync(productId, Selection("""{"portion":"full"}"""));
        variantHit!.HeatingWithheld.Should().BeFalse();
        variantHit.Heating.Should().ContainSingle(h => h.Body.Contains("35 min"));

        var fallback = await content.ResolveAsync(productId, Selection("""{"protein":"prawns"}"""));
        fallback!.HeatingWithheld.Should().BeTrue();
        fallback.Heating.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_Should_ServeTheVariantForTheNewDefault_AndFlagTheBlock_AfterADefaultMove()
    {
        // A11 — the whole §6 story: the salmon variant survives the chicken → salmon default
        // move and now serves the all-defaults selection with full declarations; the block is
        // flagged, so a now-non-default all-chicken selection is stale with declarations
        // withheld until re-upsert or confirm-review.
        var (content, productId, builder, ctx, tenantId) = await ArrangeWithTenantAsync();
        await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 640, label: "Salmon",
            ingredients: "Rice, salmon", allergens: "Fish"));

        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);
        var groupId = await builder.GroupIdAsync("protein");
        await optionService.SetRecommendedDefaultAsync(groupId, "salmon");

        // All-defaults now includes salmon → the variant wins BEFORE any block fallback.
        var allDefaults = await content.ResolveAsync(productId, null);
        allDefaults!.ServingLabel.Should().Be("Salmon");
        allDefaults.Allergens.Should().Be("Fish");
        allDefaults.IsStale.Should().BeFalse();

        // The chicken combination is now a NON-default selection resolved by the flagged block.
        var chicken = await content.ResolveAsync(productId, Selection("""{"protein":"chicken"}"""));
        chicken!.IsStale.Should().BeTrue();
        chicken.DeclarationsWithheld.Should().BeTrue();

        // Confirm-review clears the flag and re-captures the binding.
        await content.ConfirmContentReviewAsync(productId);
        var afterConfirm = await content.ResolveAsync(productId, Selection("""{"protein":"chicken"}"""));
        afterConfirm!.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_Should_TreatABindingMismatch_ExactlyLikeRequiresReview()
    {
        // §6 belt and braces — a default shifted by a path that fired no hook still can't serve
        // old standard-prep content as the new standard preparation: the stored all-defaults
        // binding disagrees with the current one, and that alone flags the resolution stale.
        var (content, productId, _, ctx) = await ArrangeAsync();

        var block = ctx.ProductContents.Single(c => c.ProductId == productId);
        block.DescribesSelectionJson = """{"protein":"beef"}""";   // a binding no hook updated
        await ctx.SaveChangesAsync();

        var resolved = await content.ResolveAsync(productId, null);

        resolved!.IsStale.Should().BeTrue();
        resolved.DeclarationsWithheld.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_Should_FallBack_When_TheVariantIsRetired_AndRestoreOnReAuthoring()
    {
        // A9 — deactivation returns the combination to fallback semantics; re-authoring the
        // retired combination revives it with no other write.
        var (content, productId, _, ctx) = await ArrangeAsync();
        var variant = await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 640, label: "Salmon"));

        await content.DeactivateVariantAsync(variant.Id);
        var retired = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        retired!.IsStandardPreparation.Should().BeTrue();
        retired.DeclarationsWithheld.Should().BeTrue();

        var revived = await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", kcal: 655, label: "Salmon v2"));
        revived.Id.Should().Be(variant.Id, "the retired row is revived, never duplicated under the unique hash");

        var restored = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        restored!.ServingLabel.Should().Be("Salmon v2");
    }

    // ─── Fixture ─────────────────────────────────────────────────────────────

    /// <summary>Jollof with the 066 catalogue (portion light*/full, protein chicken*/salmon/
    /// prawns, side, heat), all groups offered, plus an authored default block: 450 kcal,
    /// prawn-bearing ingredients, Crustaceans declaration, oven heating.</summary>
    private static async Task<(ProductContentService Content, Guid ProductId, OptionCatalogueBuilder Builder, Aonik.Commerce.Persistence.CommerceDbContext Ctx)> ArrangeAsync()
    {
        var (content, productId, builder, ctx, _) = await ArrangeWithTenantAsync();
        return (content, productId, builder, ctx);
    }

    private static async Task<(ProductContentService Content, Guid ProductId, OptionCatalogueBuilder Builder, Aonik.Commerce.Persistence.CommerceDbContext Ctx, Guid TenantId)> ArrangeWithTenantAsync()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        await builder.OfferAllAsync(productId);

        var content = CommerceTestHarness.NewContentService(ctx, tenantId);
        await content.UpsertContentAsync(productId, DefaultBlock());

        return (content, productId, builder, ctx, tenantId);
    }

    private static UpsertProductContentCommand DefaultBlock(string allergens = "Crustaceans") => new(
        "Light table 225g",
        Kcal: 450, ProteinGrams: 22, CarbsGrams: 60, FatGrams: 12,
        Ingredients: "Rice, tomato, prawn stock",
        Allergens: allergens,
        HeatingJson: """[{"method":"Oven","body":"25 min at 180C"}]""");

    private static UpsertContentVariantCommand Variant(
        string selectionJson, decimal kcal, string label,
        string? ingredients = null, string? allergens = null, string? heatingJson = null) => new(
        selectionJson, label,
        Kcal: kcal, ProteinGrams: 30, CarbsGrams: 60, FatGrams: 15,
        Ingredients: ingredients, Allergens: allergens, HeatingJson: heatingJson);

    private static JsonElement Selection(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
