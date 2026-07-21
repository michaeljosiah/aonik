using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Selection semantics: canonicalisation, difference pricing and validation (Spec 066 §7–§9).
/// Covers acceptance criteria A1–A6, A9, A11 and A16.
/// </summary>
public class OptionSelectionServiceTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<(OptionSelectionService Selections, Guid ProductId, OptionCatalogueBuilder Builder)> ArrangeAsync(
        Aonik.Commerce.Persistence.CommerceDbContext ctx, Guid tenantId)
    {
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        await builder.OfferAllAsync(productId);
        return (CommerceTestHarness.NewSelectionService(ctx, tenantId), productId, builder);
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_ReturnNegativeAdjustment_When_ChosenPriceBelowDefault()
    {
        // A1 — the default side already costs 2.00, so choosing a free side legitimately reduces
        // the total. Negative adjustments must survive end to end.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var result = await selections.NormalizeAndPriceAsync(productId, Json("""{"side":"none"}"""), "GBP");

        result.Adjustment.Should().Be(-2m);
        result.Breakdown.Should().ContainSingle(b => b.GroupKey == "side" && b.Amount == -2m);
        result.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_Reject_When_ChoiceExistsInCatalogueButProductExcludesIt()
    {
        // A2 — the decisive rule: "chicken" is a perfectly valid catalogue choice, but this fish
        // dish does not offer it.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine(
            "protein", AllowedChoiceKeys: ["salmon", "prawns"], DefaultChoiceKey: "salmon"));

        var act = () => selections.NormalizeAndPriceAsync(productId, Json("""{"protein":"chicken"}"""), "GBP");

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V2");
    }

    [Fact]
    public async Task GetEffectiveOptionsAsync_Should_ReturnEmpty_When_ProductOffersNoGroups()
    {
        // A3 — a product with no narrowing is simply not personalisable, and any non-empty
        // selection against it is a client error.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();

        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);
        var selections = CommerceTestHarness.NewSelectionService(ctx, tenantId);

        (await optionService.GetEffectiveOptionsAsync(productId)).Should().BeEmpty();

        var act = () => selections.NormalizeAndPriceAsync(productId, Json("""{"portion":"full"}"""), "GBP");
        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V1");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_SumChosenAndSubtractDefaultOnce_When_GroupIsMultiSelect()
    {
        // A4 — multi-select maths, and the canonical form's order-independence.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", SelectionModeOverride: OptionSelectionModes.Multi));

        var forward = await selections.NormalizeAndPriceAsync(productId, Json("""{"protein":["salmon","prawns"]}"""), "GBP");
        var reversed = await selections.NormalizeAndPriceAsync(productId, Json("""{"protein":["prawns","salmon"]}"""), "GBP");

        // salmon 3 + prawns 0, minus the chicken default (0), subtracted once.
        forward.Adjustment.Should().Be(3m);
        forward.CanonicalSelectionJson.Should().Be(reversed.CanonicalSelectionJson);
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_ProduceIdenticalCanonicalJson_When_GroupOrderDiffers()
    {
        // A4 (second half) — sorting the arrays is not enough; the OBJECT keys must sort too, or
        // two equivalent submissions would land as separate cart lines in Spec 068.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var a = await selections.NormalizeAndPriceAsync(productId, Json("""{"portion":"full","side":"none"}"""), "GBP");
        var b = await selections.NormalizeAndPriceAsync(productId, Json("""{"side":"none","portion":"full"}"""), "GBP");

        a.CanonicalSelectionJson.Should().Be(b.CanonicalSelectionJson);
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_Reject_When_MultiSelectGroupHasNoChoice()
    {
        // A5
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", SelectionModeOverride: OptionSelectionModes.Multi));

        var act = () => selections.NormalizeAndPriceAsync(productId, Json("""{"protein":[]}"""), "GBP");

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V4");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_FillDefaultsAndPriceZero_When_SelectionOmitted()
    {
        // A6 — a default quick-add sends no selection at all; that is not an error, and the stored
        // form is still complete so the order is self-describing.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var result = await selections.NormalizeAndPriceAsync(productId, selection: null, "GBP");

        result.IsDefault.Should().BeTrue();
        result.Adjustment.Should().Be(0m);
        result.Summary.Should().BeEmpty();
        result.CanonicalSelectionJson.Should().Be("""{"heat":"medium","portion":"light","protein":"chicken","side":"wildrice"}""");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_SnapshotLabelsForEveryGroup_Including_Defaults()
    {
        // The kitchen must be able to render the preparation from the order alone, and an
        // all-defaults order has an empty summary — so labels are snapshotted for every group.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var result = await selections.NormalizeAndPriceAsync(productId, selection: null, "GBP");

        result.Display.Should().HaveCount(4);
        result.Display.Should().Contain(d => d.Group == "Portion" && d.Choice == "Light table");
        result.Display.Should().Contain(d => d.Group == "Side" && d.Choice == "Wild rice");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_Reject_When_QuoteCurrencyDiffersFromGroupCurrency()
    {
        // A11 — nothing is converted and nothing is assumed.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var act = () => selections.NormalizeAndPriceAsync(productId, Json("""{"portion":"full"}"""), "USD");

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V10");
    }

    [Fact]
    public async Task NormalizeAsync_Should_Succeed_When_CurrenciesDiffer_Because_ContentResolutionIsNotPricing()
    {
        // The currency-free variant exists so Spec 067 content resolution cannot fail on a pricing
        // rule that has nothing to do with content.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var result = await selections.NormalizeAsync(productId, Json("""{"portion":"full"}"""));

        result.CanonicalSelectionJson.Should().Contain("\"portion\":\"full\"");
    }

    [Fact]
    public async Task RenormalizeStoredAsync_Should_RemapRetiredChoice_AndReportDrift()
    {
        // A9/A13 — a retired option must not turn every cart holding it into a hard error.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);
        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var stored = (await selections.NormalizeAndPriceAsync(productId, Json("""{"protein":"salmon"}"""), "GBP"))
            .CanonicalSelectionJson;

        var salmonId = await builder.ChoiceIdAsync("protein", "salmon");
        await optionService.UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", Price: 3m, IsActive: false));

        var result = await selections.RenormalizeStoredAsync(productId, stored, "GBP");

        result.Drift.Should().ContainSingle(d =>
            d.GroupKey == "protein" && d.FromChoiceKey == "salmon" && d.Reason == SelectionDriftReasons.OptionRetired);
        result.Result.CanonicalSelectionJson.Should().Contain("\"protein\":\"chicken\"");
        result.Result.Adjustment.Should().Be(0m);
    }

    [Fact]
    public async Task RenormalizeStoredAsync_Should_DropGroup_When_GroupNoLongerOffered()
    {
        // A deactivated group has no effective default to remap to, so remapping would invent one.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);
        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var stored = (await selections.NormalizeAndPriceAsync(productId, Json("""{"side":"none"}"""), "GBP"))
            .CanonicalSelectionJson;

        var sideId = await builder.GroupIdAsync("side");
        await optionService.UpdateGroupAsync(sideId, new UpdateOptionGroupCommand("Side", IsActive: false));

        var result = await selections.RenormalizeStoredAsync(productId, stored, "GBP");

        result.Drift.Should().ContainSingle(d => d.GroupKey == "side" && d.Reason == SelectionDriftReasons.GroupRemoved);
        result.Result.CanonicalSelectionJson.Should().NotContain("side");
    }

    [Fact]
    public async Task RenormalizeStoredAsync_Should_RemapToDefault_When_GroupTightenedToSingleSelect()
    {
        // A16 — a mode change must re-shape deterministically, never make a live cart unloadable.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", SelectionModeOverride: OptionSelectionModes.Multi));
        var stored = (await selections.NormalizeAndPriceAsync(productId, Json("""{"protein":["salmon","prawns"]}"""), "GBP"))
            .CanonicalSelectionJson;

        // Tighten back to single-select.
        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", SelectionModeOverride: OptionSelectionModes.One));

        var result = await selections.RenormalizeStoredAsync(productId, stored, "GBP");

        result.Drift.Should().ContainSingle(d =>
            d.GroupKey == "protein" && d.Reason == SelectionDriftReasons.SelectionModeChanged);
        result.Result.CanonicalSelectionJson.Should().Contain("\"protein\":\"chicken\"");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_TreatEqualPricedSwap_AsNonDefault()
    {
        // Diffs are compared by KEY, never by price — two £0 proteins are still a different
        // preparation, which is exactly what can change an allergen list (Spec 067 depends on this).
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);

        var result = await selections.NormalizeAndPriceAsync(productId, Json("""{"protein":"prawns"}"""), "GBP");

        result.Adjustment.Should().Be(0m);
        result.IsDefault.Should().BeFalse();
        result.Summary.Should().Be("King prawns");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_CarrySurchargeSeparately_From_Adjustment()
    {
        // A9 — the surcharge is independent of personalisation; both can apply to the same unit.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);
        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await optionService.SetUnitSurchargeAsync(productId, new SetUnitSurchargeCommand(4m, "GBP"));

        var result = await selections.NormalizeAndPriceAsync(productId, Json("""{"portion":"full"}"""), "GBP");

        result.Adjustment.Should().Be(10m);
        result.UnitSurcharge.Should().Be(4m);
        result.UnitSurchargeCurrency.Should().Be("GBP");
    }

    [Fact]
    public async Task RenormalizeStoredAsync_Should_InsertTheDefault_When_PartOfAMultiSelectionIsRetired()
    {
        // The drift report promised a remap to the default, so the selection must actually receive
        // it. Otherwise ["salmon","retired"] silently becomes ["salmon"] and the cart is repriced
        // against a selection the customer was never shown.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);
        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", SelectionModeOverride: OptionSelectionModes.Multi));
        var stored = (await selections.NormalizeAndPriceAsync(productId, Json("""{"protein":["salmon","prawns"]}"""), "GBP"))
            .CanonicalSelectionJson;

        var prawnsId = await builder.ChoiceIdAsync("protein", "prawns");
        await optionService.UpdateChoiceAsync(prawnsId, new UpdateOptionChoiceCommand("King prawns", IsActive: false));

        var result = await selections.RenormalizeStoredAsync(productId, stored, "GBP");

        result.Drift.Should().ContainSingle(d => d.FromChoiceKey == "prawns" && d.ToChoiceKey == "chicken");
        result.Result.CanonicalSelectionJson.Should().Contain("chicken");
        result.Result.CanonicalSelectionJson.Should().Contain("salmon");
    }

    [Fact]
    public async Task NormalizeAndPriceAsync_Should_ReportV3_When_TheChoiceWasRetiredRatherThanNeverOffered()
    {
        // V2 and V3 are different diagnoses: "you made that up" vs "we withdrew it". Collapsing
        // both into V2 leaves clients unable to tell a retirement from a bad key.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, builder) = await ArrangeAsync(ctx, tenantId);
        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var salmonId = await builder.ChoiceIdAsync("protein", "salmon");
        await optionService.UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", IsActive: false));

        var retired = () => selections.NormalizeAndPriceAsync(productId, Json("""{"protein":"salmon"}"""), "GBP");
        (await retired.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V3");

        var invented = () => selections.NormalizeAndPriceAsync(productId, Json("""{"protein":"unicorn"}"""), "GBP");
        (await invented.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V2");
    }

    [Fact]
    public async Task NormalizeAsync_Should_ReturnNoMonetaryValues_Because_NoCurrencyWasValidated()
    {
        // Without a target currency V10 has not run, so any adjustment could be a sum of different
        // denominations wearing one label. Publish the structure, not a number we cannot stand behind.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var (selections, productId, _) = await ArrangeAsync(ctx, tenantId);
        var optionService = CommerceTestHarness.NewOptionService(ctx, tenantId);
        await optionService.SetUnitSurchargeAsync(productId, new SetUnitSurchargeCommand(4m, "GBP"));

        var result = await selections.NormalizeAsync(productId, Json("""{"portion":"full"}"""));

        result.Adjustment.Should().Be(0m);
        result.Currency.Should().BeEmpty();
        result.UnitSurcharge.Should().BeNull();
        result.Breakdown.Should().BeEmpty();
        // The structural facts are still there — that is what content resolution consumes.
        result.CanonicalSelectionJson.Should().Contain("\"portion\":\"full\"");
        result.IsDefault.Should().BeFalse();
    }
}
