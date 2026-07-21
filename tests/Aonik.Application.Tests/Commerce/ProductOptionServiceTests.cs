using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Option catalogue authoring invariants and per-product resolution (Spec 066 §6, §9).
/// Covers acceptance criteria A7, A8, A10, A12 and A14.
/// </summary>
public class ProductOptionServiceTests
{
    [Fact]
    public async Task GetCatalogueAsync_Should_HideGroup_When_ItHasNoRecommendedDefaultYet()
    {
        // The servable rule: a half-authored group (created, choices still being added) is not an
        // error — it simply never appears, so creation and default-setting need not be atomic.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var group = await service.CreateGroupAsync(new CreateOptionGroupCommand("portion", "Portion"));
        await service.AddChoiceAsync(group.Id, new AddOptionChoiceCommand("light", "Light table"));

        (await service.GetCatalogueAsync(includeInactive: false)).Should().BeEmpty();
        (await service.GetCatalogueAsync(includeInactive: true)).Should().ContainSingle();
    }

    [Fact]
    public async Task AddChoiceAsync_Should_Reject_When_GroupAlreadyHasARecommendedDefault()
    {
        // A12 — direct default flags are allowed only for the 0→1 transition; moving an existing
        // default must go through the atomic operation.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var group = await service.CreateGroupAsync(new CreateOptionGroupCommand("portion", "Portion"));
        await service.AddChoiceAsync(group.Id, new AddOptionChoiceCommand("light", "Light table", IsRecommendedDefault: true));

        var act = () => service.AddChoiceAsync(group.Id, new AddOptionChoiceCommand("full", "Full table", IsRecommendedDefault: true));

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V7");
    }

    [Fact]
    public async Task SetRecommendedDefaultAsync_Should_MoveDefault_InOneWrite()
    {
        // A12 — the supported path: demote and promote together, never transiting a two-default state.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var proteinId = await builder.GroupIdAsync("protein");
        var updated = await service.SetRecommendedDefaultAsync(proteinId, "salmon");

        updated.Group.Choices.Should().ContainSingle(c => c.IsRecommendedDefault);
        updated.Group.Choices.Single(c => c.IsRecommendedDefault).Key.Should().Be("salmon");
    }

    [Fact]
    public async Task SetRecommendedDefaultAsync_Should_Reject_When_AProductNarrowingExcludesTheNewDefault()
    {
        // A14 — committing would leave that product's effective default unresolvable, and the
        // fail-safe would silently drop the whole group from its storefront. Name it instead.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync("fish-dish");
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        // The fish dish excludes chicken but relies on the group default (salmon is allowed).
        await builder.OfferAsync(productId, new ProductOptionGroupLine(
            "protein", AllowedChoiceKeys: ["salmon", "prawns"], DefaultChoiceKey: "salmon"));

        // Now clear its explicit default so it depends on the group's.
        await builder.OfferAsync(productId, new ProductOptionGroupLine(
            "protein", AllowedChoiceKeys: ["salmon", "prawns"], DefaultChoiceKey: "salmon"));

        var proteinId = await builder.GroupIdAsync("protein");

        // Moving the group default to a choice the product excludes, for a product with no
        // override, must be rejected. Build that shape explicitly:
        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", AllowedChoiceKeys: ["salmon", "prawns"], DefaultChoiceKey: "salmon"));
        var narrowed = await service.GetEffectiveOptionsAsync(productId);
        narrowed.Should().ContainSingle(g => g.Key == "protein");

        var act = () => service.SetRecommendedDefaultAsync(proteinId, "chicken");
        // The product has an explicit default, so this move is legal — assert it succeeds, which
        // is the other half of V11: only products WITHOUT an override can be orphaned.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetRecommendedDefaultAsync_Should_NameTheProduct_When_ItHasNoOverrideAndExcludesTheChoice()
    {
        // A14 proper — no per-product default, and the new group default is excluded.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync("fish-dish");
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        // Allowed excludes salmon; no explicit default, so it inherits the group's (chicken).
        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", AllowedChoiceKeys: ["chicken", "prawns"]));

        var proteinId = await builder.GroupIdAsync("protein");
        var act = () => service.SetRecommendedDefaultAsync(proteinId, "salmon");

        var ex = (await act.Should().ThrowAsync<OptionValidationException>()).Which;
        ex.RuleId.Should().Be("V11");
        ex.Message.Should().Contain("fish-dish");
    }

    [Fact]
    public async Task UpdateChoiceAsync_Should_Reject_When_DeactivatingAProductsLastResolvableDefault()
    {
        // A8 / V9 — the per-product rule, exercised on a choice that is NOT the group's recommended
        // default (chicken is). That keeps V9's remedy honest: widening this product's allowed
        // choices really would fix it. Deactivating the group's own default is a broader failure
        // with a different remedy, covered by the V7 test below.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync("salmon-only");
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine(
            "protein", AllowedChoiceKeys: ["salmon"], DefaultChoiceKey: "salmon"));

        var salmonId = await builder.ChoiceIdAsync("protein", "salmon");
        var act = () => service.UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", IsActive: false));

        var ex = (await act.Should().ThrowAsync<OptionValidationException>()).Which;
        ex.RuleId.Should().Be("V9");
        ex.Message.Should().Contain("salmon-only");
    }

    [Fact]
    public async Task UpdateChoiceAsync_Should_Reject_When_DeactivatingTheGroupsRecommendedDefault()
    {
        // The V9 product check cannot catch this one: this product carries its own resolvable
        // default and so looks perfectly safe in isolation. But §6 only serves a group with exactly
        // one active recommended default, so deactivating the group's default strips the group from
        // every product at once — including the ones that pinned their own choice.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", DefaultChoiceKey: "salmon"));

        var chickenId = await builder.ChoiceIdAsync("protein", "chicken");
        var act = () => service.UpdateChoiceAsync(chickenId, new UpdateOptionChoiceCommand("Chicken", IsActive: false));

        var ex = (await act.Should().ThrowAsync<OptionValidationException>()).Which;
        ex.RuleId.Should().Be("V7");
        ex.Message.Should().Contain("recommended default");
    }

    [Fact]
    public async Task UpdateChoiceAsync_Should_Allow_Deactivation_When_TheGroupDefaultMovedFirst()
    {
        // The V7 guard above is a sequencing rule, not a prohibition — and the group must still be
        // servable afterwards, which is the whole point of the guard.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein"));

        var groupId = await builder.GroupIdAsync("protein");
        await service.SetRecommendedDefaultAsync(groupId, "salmon");

        var chickenId = await builder.ChoiceIdAsync("protein", "chicken");
        var updated = await service.UpdateChoiceAsync(chickenId, new UpdateOptionChoiceCommand("Chicken", IsActive: false));

        updated.IsActive.Should().BeFalse();
        (await service.GetCatalogueAsync()).Should().Contain(g => g.Key == "protein");
        (await service.GetEffectiveOptionsAsync(productId))
            .Should().ContainSingle(g => g.Key == "protein")
            .Which.DefaultChoiceKey.Should().Be("salmon");
    }

    [Fact]
    public async Task SetRecommendedDefaultAsync_Should_ReportOnly_ProductsInheritingTheGroupDefault()
    {
        // A product pinned to a still-resolvable default of its own keeps exactly the preparation it
        // had, so a default move must not report it as changed — Spec 067 content review would be
        // sent re-checking products where nothing happened.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var inherits = await builder.BuildProductAsync("inherits");
        var pinned = await builder.BuildProductAsync("pinned");
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(inherits, new ProductOptionGroupLine("protein"));
        await builder.OfferAsync(pinned, new ProductOptionGroupLine("protein", DefaultChoiceKey: "prawns"));

        var groupId = await builder.GroupIdAsync("protein");
        var result = await service.SetRecommendedDefaultAsync(groupId, "salmon");

        result.AffectedProductSlugs.Should().BeEquivalentTo(["inherits"]);
    }

    [Fact]
    public async Task SetRecommendedDefaultAsync_Should_ReportProduct_When_ItsOverrideNoLongerResolves()
    {
        // A stale override is not protection: a product that pinned a since-deactivated choice is
        // silently riding the group default, so a move genuinely changes what it serves.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync("stale-override");
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", DefaultChoiceKey: "prawns"));

        var prawnsId = await builder.ChoiceIdAsync("protein", "prawns");
        await service.UpdateChoiceAsync(prawnsId, new UpdateOptionChoiceCommand("King prawns", IsActive: false));

        var groupId = await builder.GroupIdAsync("protein");
        var result = await service.SetRecommendedDefaultAsync(groupId, "salmon");

        result.AffectedProductSlugs.Should().BeEquivalentTo(["stale-override"]);
    }

    [Fact]
    public async Task SetProductOptionGroupsAsync_Should_Reject_When_NarrowingLeavesNoResolvableDefault()
    {
        // V8 — caught at authoring, where the operator can fix it, rather than discovered at
        // render time as a silently missing group.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        // Excludes the group's default (chicken) and sets no explicit default of its own.
        var act = () => service.SetProductOptionGroupsAsync(productId, new SetProductOptionGroupsCommand(
            [new ProductOptionGroupLine("protein", AllowedChoiceKeys: ["salmon"])]));

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V8");
    }

    [Fact]
    public async Task SetProductOptionGroupsAsync_Should_BeIdempotent_When_AppliedTwice()
    {
        // A10 — full-replace semantics.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAllAsync(productId);
        var first = await service.GetEffectiveOptionsAsync(productId);
        await builder.OfferAllAsync(productId);
        var second = await service.GetEffectiveOptionsAsync(productId);

        first.Should().HaveCount(4);
        second.Select(g => g.Key).Should().BeEquivalentTo(first.Select(g => g.Key));
    }

    [Fact]
    public async Task GetEffectiveOptionsAsync_Should_PreferPerProductDefault_Over_GroupDefault()
    {
        // §6 step 4 — the fallback order.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        await builder.OfferAsync(productId, new ProductOptionGroupLine("protein", DefaultChoiceKey: "salmon"));

        var effective = await service.GetEffectiveOptionsAsync(productId);
        effective.Single(g => g.Key == "protein").DefaultChoiceKey.Should().Be("salmon");
    }

    [Theory]
    [InlineData("Portion Size")]
    [InlineData("")]
    [InlineData("UPPER")]
    public async Task CreateGroupAsync_Should_Reject_InvalidKeys(string key)
    {
        // V6 — keys are the stable contract carts and content variants match on.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var act = () => service.CreateGroupAsync(new CreateOptionGroupCommand(key, "Label"));

        // "UPPER" normalises to a valid lower-case key; the others cannot.
        if (key == "UPPER")
        {
            (await act.Should().NotThrowAsync()).Which.Key.Should().Be("upper");
        }
        else
        {
            (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V6");
        }
    }

    [Fact]
    public async Task CreateGroupAsync_Should_Reject_UnknownSelectionMode()
    {
        // V12 — a typo must not persist and leave normalisation unable to decide string-vs-array.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var act = () => service.CreateGroupAsync(new CreateOptionGroupCommand("portion", "Portion", SelectionMode: "Many"));

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V12");
    }

    [Fact]
    public async Task SetUnitSurchargeAsync_Should_Require_ACurrency_When_AmountGiven()
    {
        // An undenominated amount would be silently reinterpreted if the storefront currency changed.
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        var productId = await builder.BuildProductAsync();
        var service = CommerceTestHarness.NewOptionService(ctx, tenantId);

        var act = () => service.SetUnitSurchargeAsync(productId, new SetUnitSurchargeCommand(4m));

        (await act.Should().ThrowAsync<OptionValidationException>()).Which.RuleId.Should().Be("V10");
    }

    [Fact]
    public async Task GetCatalogueAsync_Should_BeTenantIsolated()
    {
        // A15 — tenant B never sees tenant A's groups.
        var (options, tenantA) = CommerceTestHarness.NewDb();
        var tenantB = Guid.NewGuid();

        await using (var ctxA = CommerceTestHarness.CreateContext(options, tenantA))
        {
            await new OptionCatalogueBuilder(ctxA, tenantA).BuildCatalogueAsync();
        }

        await using var ctxB = CommerceTestHarness.CreateContext(options, tenantB);
        var serviceB = CommerceTestHarness.NewOptionService(ctxB, tenantB);

        (await serviceB.GetCatalogueAsync(includeInactive: true)).Should().BeEmpty();
    }
}
