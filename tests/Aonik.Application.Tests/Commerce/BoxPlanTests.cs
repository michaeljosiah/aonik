using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Spec 068 §5 pricing maths and §12 authoring rules (A1–A5).</summary>
public class BoxPlanTests
{
    private static BundleSizePlan LaunchPlan(params (int Size, decimal Price)[] presets) => new()
    {
        MinSize = 6,
        MaxSize = 30,
        BaseSize = 6,
        BasePrice = 95m,
        PerSpacePrice = 15m,
        Currency = "GBP",
        Presets = presets.Select(p => new BundleSizePreset { Size = p.Size, Price = p.Price }).ToList(),
    };

    [Fact]
    public void BoxPrice_Should_PreferPresets_AndBendTheMarginalCost()
    {
        var plan = LaunchPlan((12, 170m));

        BoxPricing.BoxPrice(plan, 6).Should().Be(95m);
        BoxPricing.BoxPrice(plan, 8).Should().Be(125m, "formula: 95 + 2×15");
        BoxPricing.BoxPrice(plan, 12).Should().Be(170m, "the preset wins over the formula's 185");

        // A19 — growing charges boxPrice(target) − boxPrice(current), never PerSpacePrice × spaces.
        (BoxPricing.BoxPrice(plan, 12) - BoxPricing.BoxPrice(plan, 11)).Should().Be(0m, "170 preset − 170 formula");
        (BoxPricing.BoxPrice(plan, 13) - BoxPricing.BoxPrice(plan, 12)).Should().Be(30m, "200 formula − 170 preset");
    }

    [Fact]
    public void BoxPrice_Should_IgnoreRetiredPresets()
    {
        var plan = LaunchPlan((12, 170m));
        plan.Presets.Single().IsDeleted = true;

        BoxPricing.BoxPrice(plan, 12).Should().Be(185m, "a soft-deleted preset no longer merchandises");
    }

    [Fact]
    public async Task Upsert_Should_EnforceTheAuthoringRules()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var plans = h.Plans();

        var cases = new (string Rule, UpsertBundleSizePlanCommand Command)[]
        {
            ("A5", new(0, 30, 6, 95m, 15m, "GBP", [])),
            ("A5", new(6, 5, 6, 95m, 15m, "GBP", [])),
            ("A2", new(6, 30, 31, 95m, 15m, "GBP", [])),
            ("A5", new(6, 30, 6, 0m, 15m, "GBP", [])),
            ("A5", new(6, 30, 6, 95m, -1m, "GBP", [])),
            ("A5", new(6, 30, 6, 95m, 15m, "POUNDS", [])),
            ("A1", new(6, 30, 6, 95m, 15m, "GBP", [new BundleSizePresetCommand(31, 200m)])),
            ("A1", new(6, 30, 6, 95m, 15m, "GBP",
                [new BundleSizePresetCommand(12, 170m), new BundleSizePresetCommand(12, 165m)])),
            ("A5", new(6, 30, 6, 95m, 15m, "GBP", [new BundleSizePresetCommand(12, 0m)])),
            // A5 — the formula must price every size above zero: at MinSize 6 with BaseSize 30
            // the formula quotes 95 − 24×15 < 0.
            ("A5", new(6, 30, 30, 95m, 15m, "GBP", [])),
        };

        foreach (var (rule, command) in cases)
        {
            var act = () => plans.UpsertAsync(f.BundleProductId, command);
            (await act.Should().ThrowAsync<StorefrontValidationException>($"case {rule}"))
                .Which.Message.Should().StartWith(rule);
        }
    }

    [Fact]
    public async Task Upsert_Should_EditPresetsInPlace_AndSurviveRemoveThenReAdd()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var plans = h.Plans();

        // Price edit at the same size — the common case; then remove and re-add the size, which
        // must not collide with the soft-deleted row (filtered unique index).
        await plans.UpsertAsync(f.BundleProductId, new(6, 30, 6, 95m, 15m, "GBP",
            [new BundleSizePresetCommand(12, 165m)]));
        await plans.UpsertAsync(f.BundleProductId, new(6, 30, 6, 95m, 15m, "GBP", []));
        var revived = await plans.UpsertAsync(f.BundleProductId, new(6, 30, 6, 95m, 15m, "GBP",
            [new BundleSizePresetCommand(12, 175m)]));

        revived.Presets.Single().Price.Should().Be(175m);
    }

    [Fact]
    public async Task CurrencyChange_Should_BeBlocked_WhileOpenBoxSessionsExist()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));

        var act = () => h.Plans().UpsertAsync(f.BundleProductId, new(6, 30, 6, 95m, 15m, "EUR", []));
        (await act.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().StartWith("A4");

        // Abandoned sessions no longer pin the currency (A4 counts Open only; the A6 sweep
        // exists precisely so staleness cannot pin authoring forever).
        await h.Maintenance().AbandonIdleBoxCartsAsync(DateTime.UtcNow.AddDays(15));
        var after = await h.Plans().UpsertAsync(f.BundleProductId, new(6, 30, 6, 95m, 15m, "EUR", []));
        after.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task GenericBundlePricing_Should_RejectSizeTieredBundles()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");

        var act = () => h.Pricing().ResolveBundlePriceAsync(
            f.BundleProductId, [new BundleSelectionLine(f.SlotId, f.DishVariants["jollof"], 1m)], "GBP");

        (await act.Should().ThrowAsync<StorefrontValidationException>())
            .Which.Message.Should().Contain("size plan");
    }
}
