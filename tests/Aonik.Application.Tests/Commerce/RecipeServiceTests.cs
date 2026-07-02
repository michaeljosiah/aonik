using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Production;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Recipe / bill-of-materials definition, validation, replace-in-place, and explosion
/// (Spec 050 §8/§11, R2–R5). InMemory cannot enforce the filtered unique indexes — these tests
/// prove the service validation; the indexes are belt-and-braces for SQL Server.
/// </summary>
public class RecipeServiceTests
{
    private static (RecipeService Service, CommerceDbContext Ctx) Build(
        DbContextOptions<CommerceDbContext> options, Guid tenantId)
    {
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var svc = new RecipeService(ctx, new TestTenantProvider(tenantId));
        return (svc, ctx);
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

    private static async Task<Ingredient> SeedIngredientAsync(
        CommerceDbContext ctx, Guid tenantId, string name, string baseUnit = IngredientBaseUnits.Kg, bool isActive = true)
    {
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            BaseUnit = baseUnit,
            IsActive = isActive,
        };
        ctx.Ingredients.Add(ingredient);
        await ctx.SaveChangesAsync();
        return ingredient;
    }

    [Fact]
    public async Task SetRecipe_Then_GetRecipe_Should_Roundtrip()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");
        var tomato = await SeedIngredientAsync(ctx, tenantId, "Tomato");

        var set = await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
            new RecipeComponentCommand(tomato.Id, 0.5m, "blended"),
        }));

        set.ProductVariantId.Should().Be(jollof);
        set.YieldQuantity.Should().Be(4m);
        set.IsActive.Should().BeTrue();

        var read = await svc.GetRecipeAsync(jollof);
        read.Should().NotBeNull();
        read!.Id.Should().Be(set.Id);
        read.Name.Should().Be("Jollof rice");
        read.YieldUnit.Should().Be("portion");
        read.Components.Should().HaveCount(2);

        var riceLine = read.Components.Single(c => c.IngredientId == rice.Id);
        riceLine.IngredientName.Should().Be("Rice");
        riceLine.BaseUnit.Should().Be("kg");
        riceLine.Quantity.Should().Be(1m);
        read.Components.Single(c => c.IngredientId == tomato.Id).Notes.Should().Be("blended");
    }

    [Fact]
    public async Task SetRecipe_Should_ReplaceInPlace_WhenActiveRecipeExists()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");
        var tomato = await SeedIngredientAsync(ctx, tenantId, "Tomato");
        var pepper = await SeedIngredientAsync(ctx, tenantId, "Pepper");

        var first = await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
            new RecipeComponentCommand(tomato.Id, 0.5m),
        }));

        var second = await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice v2", 10m, "portion", new[]
        {
            new RecipeComponentCommand(pepper.Id, 0.2m),
        }));

        // Replaced under the same audited entity (R2) — never a second active recipe (R3).
        second.Id.Should().Be(first.Id);
        second.Name.Should().Be("Jollof rice v2");
        second.YieldQuantity.Should().Be(10m);
        second.Components.Should().ContainSingle(c => c.IngredientId == pepper.Id);

        // Verify through a fresh context: one recipe row, and the old component rows are gone
        // from the live view.
        await using var verify = CommerceTestHarness.CreateContext(options, tenantId);
        (await verify.Recipes.CountAsync()).Should().Be(1);
        var liveComponents = await verify.RecipeComponents.Where(c => c.RecipeId == first.Id).ToListAsync();
        liveComponents.Should().ContainSingle().Which.IngredientId.Should().Be(pepper.Id);
    }

    [Fact]
    public async Task SetRecipe_Should_MergeDuplicateComponentEntries()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");

        var set = await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 0.4m),
            new RecipeComponentCommand(rice.Id, 0.6m),
        }));

        var line = set.Components.Should().ContainSingle().Subject;
        line.IngredientId.Should().Be(rice.Id);
        line.Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task SetRecipe_Should_Throw_WhenIngredientInactive()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var oldStock = await SeedIngredientAsync(ctx, tenantId, "Old stock", isActive: false);

        var act = async () => await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(oldStock.Id, 1m),
        }));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task SetRecipe_Should_Throw_WhenIngredientUnknown()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");

        var act = async () => await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(Guid.NewGuid(), 1m),
        }));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public async Task SetRecipe_Should_Throw_WhenQuantityNotPositive()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");

        var act = async () => await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 0m),
        }));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must be positive*");
    }

    [Fact]
    public async Task SetRecipe_Should_Throw_WhenVariantUnknown()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");

        var act = async () => await svc.SetRecipeAsync(new SetRecipeCommand(Guid.NewGuid(), "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
        }));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*variant*was not found*");
    }

    [Fact]
    public async Task SetRecipe_Should_Throw_WhenYieldNotPositive()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");

        var act = async () => await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 0m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
        }));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*yield quantity*");
    }

    [Fact]
    public async Task Explode_Should_ScaleByPortionsOverYield()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        // The spec's verification example (§17): a jollof recipe yielding 4 portions from
        // 1 kg rice + 0.5 kg tomato explodes for 40 portions to 10 kg rice + 5 kg tomato.
        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");
        var tomato = await SeedIngredientAsync(ctx, tenantId, "Tomato");

        await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
            new RecipeComponentCommand(tomato.Id, 0.5m),
        }));

        var explosion = await svc.ExplodeAsync(jollof, 40m);

        explosion.HasActiveRecipe.Should().BeTrue();
        explosion.Portions.Should().Be(40m);
        explosion.Lines.Should().HaveCount(2);
        explosion.Lines.Single(l => l.IngredientId == rice.Id).RequiredQuantity.Should().Be(10m);
        explosion.Lines.Single(l => l.IngredientId == tomato.Id).RequiredQuantity.Should().Be(5m);
        explosion.Lines.Should().OnlyContain(l => l.BaseUnit == "kg");
    }

    [Fact]
    public async Task Explode_Should_FlagNoActiveRecipe_WhenVariantHasNone()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = await SeedVariantAsync(ctx, tenantId, "NO-RECIPE");

        var explosion = await svc.ExplodeAsync(variant, 10m);

        // Never a silent zero (R5) — the caller can surface "no recipe defined".
        explosion.HasActiveRecipe.Should().BeFalse();
        explosion.Lines.Should().BeEmpty();

        (await svc.GetRecipeAsync(variant)).Should().BeNull();
    }

    [Fact]
    public async Task ExplodeMany_Should_MergeAcrossVariants_SummingSharedIngredients()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var steak = await SeedVariantAsync(ctx, tenantId, "STEAK");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");
        var tomato = await SeedIngredientAsync(ctx, tenantId, "Tomato");
        var steakCut = await SeedIngredientAsync(ctx, tenantId, "Steak cut");

        await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
            new RecipeComponentCommand(tomato.Id, 0.5m),
        }));
        await svc.SetRecipeAsync(new SetRecipeCommand(steak, "Seared steak", 1m, "portion", new[]
        {
            new RecipeComponentCommand(steakCut.Id, 0.3m),
            new RecipeComponentCommand(rice.Id, 0.05m), // rice side — shared with jollof
        }));

        var bom = await svc.ExplodeManyAsync(new[]
        {
            new VariantDemand(jollof, 40m),
            new VariantDemand(steak, 30m),
        });

        bom.VariantsWithoutRecipe.Should().BeEmpty();
        bom.Lines.Should().HaveCount(3);
        // Rice: 40/4 * 1 + 30/1 * 0.05 = 10 + 1.5 = 11.5 kg (shared ingredient summed).
        bom.Lines.Single(l => l.IngredientId == rice.Id).RequiredQuantity.Should().Be(11.5m);
        bom.Lines.Single(l => l.IngredientId == tomato.Id).RequiredQuantity.Should().Be(5m);
        bom.Lines.Single(l => l.IngredientId == steakCut.Id).RequiredQuantity.Should().Be(9m);
    }

    [Fact]
    public async Task ExplodeMany_Should_ReportVariantsWithoutRecipe()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var jollof = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var mystery = await SeedVariantAsync(ctx, tenantId, "MYSTERY");
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");

        await svc.SetRecipeAsync(new SetRecipeCommand(jollof, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
        }));

        var bom = await svc.ExplodeManyAsync(new[]
        {
            new VariantDemand(jollof, 8m),
            new VariantDemand(mystery, 5m),
        });

        // The variant without a recipe is reported, never silently under-counted (R5).
        bom.VariantsWithoutRecipe.Should().ContainSingle().Which.Should().Be(mystery);
        bom.Lines.Single(l => l.IngredientId == rice.Id).RequiredQuantity.Should().Be(2m);
    }
}
