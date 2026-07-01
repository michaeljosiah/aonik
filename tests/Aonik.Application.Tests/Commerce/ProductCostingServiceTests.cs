using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Production;
using Aonik.Commerce.Services.Sourcing;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Standard-cost rollup (Spec 051 §9/§10, R5–R7): Spec 050 explosion × date-aware ingredient cost,
/// per yield-unit; the no-recipe and missing-cost diagnostics (total withheld, never a silent zero
/// or partial); single-currency valuation with no FX conversion; and a scheduled (future-dated)
/// cost never pricing today's rollup.
/// </summary>
public class ProductCostingServiceTests
{
    private sealed record Fixture(
        ProductCostingService Costing,
        RecipeService Recipes,
        IngredientCostService Costs,
        CommerceDbContext Ctx,
        CommerceTestHarness.TestClock Clock);

    private static Fixture Build(DbContextOptions<CommerceDbContext> options, Guid tenantId)
    {
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var clock = new CommerceTestHarness.TestClock();
        var tenant = new TestTenantProvider(tenantId);
        var recipes = new RecipeService(ctx, tenant);
        var costs = new IngredientCostService(ctx, tenant, clock);
        var costing = new ProductCostingService(recipes, costs, clock);
        return new Fixture(costing, recipes, costs, ctx, clock);
    }

    private static async Task<Guid> SeedVariantAsync(CommerceDbContext ctx, Guid tenantId, string sku)
    {
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = Guid.NewGuid(),
            Sku = sku,
            Name = sku,
        };
        ctx.ProductVariants.Add(variant);
        await ctx.SaveChangesAsync();
        return variant.Id;
    }

    private static async Task<Ingredient> SeedIngredientAsync(CommerceDbContext ctx, Guid tenantId, string name)
    {
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            BaseUnit = IngredientBaseUnits.Kg,
            IsActive = true,
        };
        ctx.Ingredients.Add(ingredient);
        await ctx.SaveChangesAsync();
        return ingredient;
    }

    /// <summary>The hand-computed jollof example: yield 4 portions from 1 kg rice + 0.5 kg tomato
    /// ⇒ per portion 0.25 kg rice + 0.125 kg tomato.</summary>
    private static async Task<(Guid Jollof, Ingredient Rice, Ingredient Tomato)> SeedJollofAsync(Fixture f, Guid tenantId)
    {
        var jollof = await SeedVariantAsync(f.Ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(f.Ctx, tenantId, "Rice");
        var tomato = await SeedIngredientAsync(f.Ctx, tenantId, "Tomato");

        await f.Recipes.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
            new RecipeComponentCommand(tomato.Id, 0.5m),
        }));

        return (jollof, rice, tomato);
    }

    [Fact]
    public async Task Rollup_Should_ComputePerPortionCost_FromRecipeAndCurrentCosts()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var f = Build(options, tenantId);
        await using var _ctx = f.Ctx;

        var (jollof, rice, tomato) = await SeedJollofAsync(f, tenantId);
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));   // ₦/kg
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(tomato.Id, "NGN", 800m));   // ₦/kg

        var result = await f.Costing.RollupStandardCostAsync(jollof, "NGN");

        // Per portion: 0.25 kg rice × ₦1,200 + 0.125 kg tomato × ₦800 = 300 + 100 = ₦400 (R5).
        result.ProductVariantId.Should().Be(jollof);
        result.Currency.Should().Be("NGN");
        result.AsOfUtc.Should().Be(f.Clock.UtcNow);
        result.HasActiveRecipe.Should().BeTrue();
        result.CostComplete.Should().BeTrue();
        result.UnitCost.Should().Be(400m);
        result.Lines.Should().HaveCount(2);

        var riceLine = result.Lines.Single(l => l.IngredientId == rice.Id);
        riceLine.IngredientName.Should().Be("Rice");
        riceLine.BaseUnit.Should().Be("kg");
        riceLine.QuantityPerYieldUnit.Should().Be(0.25m);
        riceLine.UnitCost.Should().Be(1_200m);
        riceLine.LineCost.Should().Be(300m);
        riceLine.HasCost.Should().BeTrue();

        var tomatoLine = result.Lines.Single(l => l.IngredientId == tomato.Id);
        tomatoLine.QuantityPerYieldUnit.Should().Be(0.125m);
        tomatoLine.UnitCost.Should().Be(800m);
        tomatoLine.LineCost.Should().Be(100m);
        tomatoLine.HasCost.Should().BeTrue();
    }

    [Fact]
    public async Task Rollup_Should_WithholdTotal_AndFlagLine_WhenComponentHasNoCost()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var f = Build(options, tenantId);
        await using var _ctx = f.Ctx;

        var (jollof, rice, tomato) = await SeedJollofAsync(f, tenantId);
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));
        // Tomato deliberately has no cost.

        var result = await f.Costing.RollupStandardCostAsync(jollof, "NGN");

        // Never a silent zero (R6): the total is withheld, the unpriced line is flagged, and the
        // priced line keeps its breakdown.
        result.HasActiveRecipe.Should().BeTrue();
        result.CostComplete.Should().BeFalse();
        result.UnitCost.Should().BeNull();

        var tomatoLine = result.Lines.Single(l => l.IngredientId == tomato.Id);
        tomatoLine.HasCost.Should().BeFalse();
        tomatoLine.UnitCost.Should().BeNull();
        tomatoLine.LineCost.Should().BeNull();

        var riceLine = result.Lines.Single(l => l.IngredientId == rice.Id);
        riceLine.HasCost.Should().BeTrue();
        riceLine.LineCost.Should().Be(300m);
    }

    [Fact]
    public async Task Rollup_Should_FlagNoActiveRecipe_WhenVariantHasNone()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var f = Build(options, tenantId);
        await using var _ctx = f.Ctx;

        var variant = await SeedVariantAsync(f.Ctx, tenantId, "NO-RECIPE");

        var result = await f.Costing.RollupStandardCostAsync(variant, "NGN");

        // "No recipe defined" is a surfaced diagnostic, not a ₦0 cost (R6).
        result.HasActiveRecipe.Should().BeFalse();
        result.CostComplete.Should().BeFalse();
        result.UnitCost.Should().BeNull();
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Rollup_Should_FlagLines_WhenCostExistsOnlyInDifferentCurrency()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var f = Build(options, tenantId);
        await using var _ctx = f.Ctx;

        var (jollof, rice, tomato) = await SeedJollofAsync(f, tenantId);
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(tomato.Id, "NGN", 800m));

        // One currency per rollup call (§10/R7): NGN costs never price a USD rollup — the lines
        // are flagged, never FX-converted.
        var result = await f.Costing.RollupStandardCostAsync(jollof, "USD");

        result.Currency.Should().Be("USD");
        result.HasActiveRecipe.Should().BeTrue();
        result.CostComplete.Should().BeFalse();
        result.UnitCost.Should().BeNull();
        result.Lines.Should().HaveCount(2);
        result.Lines.Should().OnlyContain(l => !l.HasCost && l.UnitCost == null && l.LineCost == null);
    }

    [Fact]
    public async Task Rollup_Should_IgnoreScheduledCost_UntilItsDateArrives()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var f = Build(options, tenantId);
        await using var _ctx = f.Ctx;

        var (jollof, rice, tomato) = await SeedJollofAsync(f, tenantId);
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(tomato.Id, "NGN", 800m));

        // A supplier reprice scheduled for tomorrow (R4): rice ₦1,200 → ₦2,000.
        var tomorrow = f.Clock.UtcNow.AddDays(1);
        await f.Costs.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 2_000m, tomorrow));

        // Today's rollup still prices rice at ₦1,200 — the scheduled row never leaks (R4).
        var today = await f.Costing.RollupStandardCostAsync(jollof, "NGN");
        today.CostComplete.Should().BeTrue();
        today.UnitCost.Should().Be(400m);
        today.Lines.Single(l => l.IngredientId == rice.Id).UnitCost.Should().Be(1_200m);

        // The same call valued at tomorrow uses the scheduled cost: 0.25×2,000 + 0.125×800 = 600.
        var atTomorrow = await f.Costing.RollupStandardCostAsync(jollof, "NGN", tomorrow);
        atTomorrow.UnitCost.Should().Be(600m);

        // Once the clock passes the boundary, "now" flips with no further write.
        f.Clock.UtcNow = tomorrow.AddHours(2);
        var afterBoundary = await f.Costing.RollupStandardCostAsync(jollof, "NGN");
        afterBoundary.UnitCost.Should().Be(600m);
        afterBoundary.Lines.Single(l => l.IngredientId == rice.Id).UnitCost.Should().Be(2_000m);
    }
}
