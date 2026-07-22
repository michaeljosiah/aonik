using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
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
    public async Task ANarrowingWrite_Should_FlagTheBlock_OnlyWhenTheEffectiveDefaultChanges()
    {
        // §6, precise form — a default-shifting narrowing flags; an idempotent re-offer or a
        // pure allowed-set edit that leaves every effective default intact must NOT withhold
        // allergen declarations until an admin performs a pointless review.
        var (content, productId, builder, ctx) = await ArrangeAsync();

        // Idempotent re-offer: same groups, same defaults → no flag.
        await builder.OfferAllAsync(productId);
        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeFalse("nothing about the standard preparation changed");

        // Allowed-set narrowing that keeps every group offered and every effective default
        // intact → still no flag. (OfferAsync is a FULL replace, so all four groups restate.)
        await builder.OfferAsync(productId,
            new ProductOptionGroupLine("portion"),
            new ProductOptionGroupLine("protein", AllowedChoiceKeys: ["chicken", "salmon"]),
            new ProductOptionGroupLine("side"),
            new ProductOptionGroupLine("heat"));
        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeFalse("chicken is still the effective default of a still-offered group");

        // Explicit-default change → flag.
        await builder.OfferAsync(productId,
            new ProductOptionGroupLine("portion"),
            new ProductOptionGroupLine("protein", AllowedChoiceKeys: ["salmon", "prawns"], DefaultChoiceKey: "salmon"),
            new ProductOptionGroupLine("side"),
            new ProductOptionGroupLine("heat"));
        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview.Should().BeTrue();
    }

    [Fact]
    public async Task GroupLevelEdits_Should_FlagInheritingProducts_WhenTheyChangeTheSelection()
    {
        // §6 — deactivating a group removes it from every product's all-defaults selection; a
        // mode change alters its canonical shape. Label-only edits change nothing.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var optionService = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var groupId = await builder.GroupIdAsync("heat");

        await optionService.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat (renamed)"));
        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeFalse("a rename changes no selection");

        await optionService.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: false));
        var block = ctx.ProductContents.Single(c => c.ProductId == productId);
        block.RequiresReview.Should().BeTrue("the group vanished from the standard preparation");
    }

    [Fact]
    public async Task DeactivatingAPinnedExplicitDefault_Should_FlagTheProduct()
    {
        // §6 — V9 permits deactivating a product's explicit default when the group default
        // remains, silently falling the product back to it: that IS a standard-preparation
        // change, and the content block must be flagged in the same write.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var optionService = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", DefaultChoiceKey: "salmon"));
        await content.ConfirmContentReviewAsync(productId); // clear the narrowing-change flag

        var salmonId = await builder.ChoiceIdAsync("protein", "salmon");
        await optionService.UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", IsActive: false));

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeTrue("the effective default silently fell back to the group default");
    }

    [Fact]
    public async Task OneSidedDeclarations_Should_StillReportWithheld()
    {
        // ANY missing declaration is withheld: ingredients authored with no allergens must show
        // the not-yet-published state — the absent ALLERGEN line is the dangerous half. The
        // authored side still serves.
        var (content, productId, _, _) = await ArrangeAsync();
        await content.UpsertContentAsync(productId, DefaultBlock() with { Allergens = null });

        var block = await content.ResolveAsync(productId, null);
        block!.Ingredients.Should().NotBeNull();
        block.Allergens.Should().BeNull();
        block.DeclarationsWithheld.Should().BeTrue();

        await content.AddVariantAsync(productId, new UpsertContentVariantCommand(
            """{"protein":"salmon"}""", "Salmon",
            Kcal: 640, ProteinGrams: 30, CarbsGrams: 60, FatGrams: 12,
            Ingredients: "Rice, salmon" /* allergens deliberately unauthored */));

        var variant = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        variant!.Ingredients.Should().Be("Rice, salmon");
        variant.DeclarationsWithheld.Should().BeTrue("no exact allergen declaration exists for this combination");
    }

    [Fact]
    public async Task GroupServabilityFlips_Should_FlagEveryOfferingProduct()
    {
        // M1's real path: an active group left with zero active recommended defaults is
        // unservable (absent from every all-defaults selection); reactivating that sole default
        // flips servability back and re-enters every offering product's standard preparation —
        // INCLUDING products whose narrowing pins a DIFFERENT choice, which the old
        // pinned-products filter never staged.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var optionService = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var groupId = await builder.GroupIdAsync("heat");
        var mediumId = await builder.ChoiceIdAsync("heat", "medium");   // heat's recommended default

        // Pin this product's heat default to "high" (full replace restates every line). The
        // product never references "medium", so DefaultChoiceKey-based staging misses it.
        await builder.OfferAsync(productId,
            new ProductOptionGroupLine("portion", SortOrder: 0),
            new ProductOptionGroupLine("protein", SortOrder: 1),
            new ProductOptionGroupLine("side", SortOrder: 2),
            new ProductOptionGroupLine("heat", DefaultChoiceKey: "high", SortOrder: 3));

        // Reach "active group, zero active defaults" via the permitted route: deactivate the
        // group (V7 guards only active groups), retire its group default (V9 satisfied — the
        // narrowing resolves via its pinned "high"), reactivate the still-unservable group.
        await optionService.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: false));
        await optionService.UpdateChoiceAsync(mediumId, new UpdateOptionChoiceCommand("Medium", IsActive: false));
        await optionService.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: true));
        await content.ConfirmContentReviewAsync(productId);   // clear the flags those staged

        // Reactivating the sole recommended default makes the group servable again → every
        // offering product is staged, pinned-elsewhere narrowings included.
        await optionService.UpdateChoiceAsync(mediumId, new UpdateOptionChoiceCommand("Medium", IsActive: true));

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeTrue("the group re-entered the product's standard preparation");
    }

    [Fact]
    public async Task InactiveGroupModeEdits_Should_NotFlag()
    {
        // M3 — an unservable group is absent from every all-defaults selection; changing its
        // mode changes nothing a customer sees, and must not withhold declarations for review.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var optionService = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var groupId = await builder.GroupIdAsync("heat");

        await optionService.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: false));
        await content.ConfirmContentReviewAsync(productId);   // clear the deactivation flag

        await optionService.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand(
            "Heat", SelectionMode: OptionSelectionModes.Multi));

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeFalse("an unservable group contributes nothing to the selection");
    }

    [Fact]
    public async Task BlankDeclarations_Should_NormalizeToAbsent()
    {
        // "" is absence wearing quotes: storing it would report DeclarationsWithheld: false over
        // no usable allergen information, suppressing the storefront's unpublished warning.
        var (content, productId, _, _) = await ArrangeAsync();

        await content.UpsertContentAsync(productId, DefaultBlock() with { Ingredients = "  ", Allergens = "" });

        var resolved = await content.ResolveAsync(productId, null);
        resolved!.Ingredients.Should().BeNull();
        resolved.Allergens.Should().BeNull();
        resolved.DeclarationsWithheld.Should().BeTrue();
    }

    [Fact]
    public async Task MalformedStoredHeating_Should_BeWithheld_NotServedAsAuthoredEmpty()
    {
        // Legacy damage is withheld, never presented as an explicitly authored "no heating
        // required" — the same authored-or-absent rule as the declarations.
        var (content, productId, _, ctx) = await ArrangeAsync();
        var block = ctx.ProductContents.Single(c => c.ProductId == productId);
        block.HeatingJson = "{corrupt";
        await ctx.SaveChangesAsync();

        var resolved = await content.ResolveAsync(productId, null);

        resolved!.HeatingWithheld.Should().BeTrue();
        resolved.Heating.Should().BeEmpty();
        resolved.DeclarationsWithheld.Should().BeFalse("only the corrupted panel is withheld");
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

    // ─── Round-3: servability-aware staging on the remaining mutation paths ──────────────

    private static async Task<(ProductContentService Content, Guid ProductId,
        Aonik.Commerce.Persistence.CommerceDbContext Ctx, ProductOptionService Options, Guid HeatGroupId)>
        ArrangeActiveButUnservableHeatAsync()
    {
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var options = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var groupId = await builder.GroupIdAsync("heat");
        var mediumId = await builder.ChoiceIdAsync("heat", "medium");

        // Pin heat to "high" so the product never references "medium", then walk the permitted
        // route to "active group, zero active recommended defaults" (see
        // GroupServabilityFlips_Should_FlagEveryOfferingProduct).
        await builder.OfferAsync(productId,
            new ProductOptionGroupLine("portion", SortOrder: 0),
            new ProductOptionGroupLine("protein", SortOrder: 1),
            new ProductOptionGroupLine("side", SortOrder: 2),
            new ProductOptionGroupLine("heat", DefaultChoiceKey: "high", SortOrder: 3));
        await options.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: false));
        await options.UpdateChoiceAsync(mediumId, new UpdateOptionChoiceCommand("Medium", IsActive: false));
        await options.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: true));
        await content.ConfirmContentReviewAsync(productId);

        return (content, productId, ctx, options, groupId);
    }

    [Fact]
    public async Task AddChoice_RestoringServability_Should_FlagEveryOfferingProduct()
    {
        // The one add-side default transition V7 sanctions (0 → 1) flips an active group
        // servable; every offering product's standard preparation gains the group, so the add
        // must stage them — a cached ?v=N response would otherwise keep serving the previous
        // declarations under an unchanged version.
        var (_, productId, ctx, options, groupId) = await ArrangeActiveButUnservableHeatAsync();

        await options.AddChoiceAsync(groupId, new AddOptionChoiceCommand(
            "scorching", "Scorching", IsRecommendedDefault: true));

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeTrue("the new active default made the group servable again");
    }

    [Fact]
    public async Task DefaultMove_RestoringServability_Should_FlagPinnedProductsToo()
    {
        // Promoting a default on an active-but-unservable group restores it for EVERY offering
        // product — including one pinned to a different choice, which inheriting-only staging
        // never covered.
        var (_, productId, ctx, options, groupId) = await ArrangeActiveButUnservableHeatAsync();

        await options.SetRecommendedDefaultAsync(groupId, "high");

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeTrue("the move made the group servable and the pinned narrowing regained it");
    }

    [Fact]
    public async Task DefaultMove_OnInactiveGroup_Should_NotFlag()
    {
        // The group is absent from every selection before and after the move — maintaining an
        // inactive group must not withhold declarations pending a pointless review.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var options = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var groupId = await builder.GroupIdAsync("heat");

        await options.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: false));
        await content.ConfirmContentReviewAsync(productId);

        await options.SetRecommendedDefaultAsync(groupId, "high");

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeFalse("an inactive group contributes nothing to any standard preparation");
    }

    [Fact]
    public async Task PinnedChoiceFlips_OnUnservableGroup_Should_NotFlag()
    {
        // Deactivating a pinned choice while its group is inactive changes nothing customers
        // see; V9 still guards resolvability (the group default remains), and staging waits
        // for the group's own reactivation path.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var options = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var groupId = await builder.GroupIdAsync("heat");
        var highId = await builder.ChoiceIdAsync("heat", "high");

        await builder.OfferAsync(productId,
            new ProductOptionGroupLine("portion", SortOrder: 0),
            new ProductOptionGroupLine("protein", SortOrder: 1),
            new ProductOptionGroupLine("side", SortOrder: 2),
            new ProductOptionGroupLine("heat", DefaultChoiceKey: "high", SortOrder: 3));
        await options.UpdateGroupAsync(groupId, new UpdateOptionGroupCommand("Heat", IsActive: false));
        await content.ConfirmContentReviewAsync(productId);

        await options.UpdateChoiceAsync(highId, new UpdateOptionChoiceCommand("High", IsActive: false));

        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview
            .Should().BeFalse("an unservable group's pinned default is not being served");
    }

    [Fact]
    public async Task VariantWhoseCombinationBecameDefault_Should_RemainUpdatable()
    {
        // A default move under an authored variant makes its combination the standard one;
        // resolution keeps serving the variant ahead of the block, so its facts must stay
        // correctable in place — while MOVING another variant onto the default combination
        // stays V-C1.
        var (content, productId, builder, ctx) = await ArrangeAsync();
        var options = CommerceTestHarness.NewOptionService(ctx, TenantOf(ctx, productId));
        var salmonVariant = await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));
        var prawnVariant = await content.AddVariantAsync(productId, Variant("""{"protein":"prawns"}""", 610));

        await options.SetRecommendedDefaultAsync(await builder.GroupIdAsync("protein"), "salmon");

        var updated = await content.UpdateVariantAsync(salmonVariant.Id, Variant("""{"protein":"salmon"}""", 655));
        updated.Nutrition.Kcal.Should().Be(655);

        var move = () => content.UpdateVariantAsync(prawnVariant.Id, Variant("""{"protein":"salmon"}""", 700));
        (await move.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C1");
    }

    [Fact]
    public async Task RetiredVariants_Should_RejectUpdates()
    {
        // Soft-retired rows are history for audit and revival — editing one in place would
        // rewrite that record while staying unservable. Revive goes through the add path.
        var (content, productId, _, _) = await ArrangeAsync();
        var variant = await content.AddVariantAsync(productId, Variant("""{"protein":"salmon"}""", 640));
        await content.DeactivateVariantAsync(variant.Id);

        var act = () => content.UpdateVariantAsync(variant.Id, Variant("""{"protein":"salmon"}""", 650));

        (await act.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("V-C5");
    }

    private static Guid TenantOf(Aonik.Commerce.Persistence.CommerceDbContext ctx, Guid productId)
        => ctx.Products.Single(p => p.Id == productId).TenantId;

    private static JsonElement Selection(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static UpsertProductContentCommand DefaultBlock() => new(
        "Light table 225g",
        Kcal: 450, ProteinGrams: 22, CarbsGrams: 60, FatGrams: 12,
        Ingredients: "Rice, tomato, prawn stock",
        Allergens: "Crustaceans");

    private static UpsertContentVariantCommand Variant(string selectionJson, decimal kcal) => new(
        selectionJson, "Variant serving",
        Kcal: kcal, ProteinGrams: 30, CarbsGrams: 60, FatGrams: 15);
}
