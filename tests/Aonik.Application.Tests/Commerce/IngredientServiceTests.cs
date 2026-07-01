using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Production;
using Aonik.Commerce.Services.Sourcing;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Ingredient (raw-material) master lifecycle (Spec 050 §8/R1): create, list, update,
/// deactivate — including the guards that protect live recipes from base-unit changes and
/// deactivation of ingredients they reference.</summary>
public class IngredientServiceTests
{
    private static (IngredientService Service, CommerceDbContext Ctx) Build(
        DbContextOptions<CommerceDbContext> options, Guid tenantId)
    {
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var svc = new IngredientService(ctx, new TestTenantProvider(tenantId));
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

    /// <summary>Sets an active recipe (via the real <see cref="RecipeService"/>, as RecipeServiceTests
    /// does) referencing each ingredient with quantity 1 in its base unit.</summary>
    private static async Task SeedActiveRecipeAsync(
        CommerceDbContext ctx, Guid tenantId, string recipeName, params Guid[] ingredientIds)
    {
        var variantId = await SeedVariantAsync(ctx, tenantId, $"VAR-{Guid.NewGuid():N}");
        var recipes = new RecipeService(ctx, new TestTenantProvider(tenantId));
        await recipes.SetRecipeAsync(new SetRecipeCommand(
            variantId, recipeName, 4m, "portion",
            ingredientIds.Select(id => new RecipeComponentCommand(id, 1m)).ToList()));
    }

    [Fact]
    public async Task Create_Should_PersistIngredient_AndListReturnsIt()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg", Sku: "ING-TOM", Category: "Produce"));
        var rice = await svc.CreateAsync(new CreateIngredientCommand("Basmati rice", "kg"));

        rice.Name.Should().Be("Basmati rice");
        rice.BaseUnit.Should().Be("kg");
        rice.IsActive.Should().BeTrue();

        var list = await svc.ListAsync();
        list.Should().HaveCount(2);
        // Ordered by name.
        list[0].Name.Should().Be("Basmati rice");
        list[1].Name.Should().Be("Tomato");
        list[1].Sku.Should().Be("ING-TOM");
        list[1].Category.Should().Be("Produce");
    }

    [Fact]
    public async Task Create_Should_Throw_WhenDuplicateNameInTenant()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));

        var act = async () => await svc.CreateAsync(new CreateIngredientCommand("Tomato", "g"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tomato*already exists*");
    }

    [Fact]
    public async Task Create_Should_Throw_WhenBaseUnitMissing()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var act = async () => await svc.CreateAsync(new CreateIngredientCommand("Tomato", "  "));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Update_Should_OverwriteMasterData()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var created = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));

        var updated = await svc.UpdateAsync(new UpdateIngredientCommand(
            created.Id, "Plum tomato", "g", Sku: "ING-PT", Category: "Produce", Notes: "prefer ripe"));

        updated.Id.Should().Be(created.Id);
        updated.Name.Should().Be("Plum tomato");
        updated.BaseUnit.Should().Be("g");
        updated.Sku.Should().Be("ING-PT");
        updated.Notes.Should().Be("prefer ripe");
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_Should_HideFromDefaultList_ButKeepInInactiveList()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var tomato = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));
        await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));

        await svc.DeactivateAsync(tomato.Id);

        var active = await svc.ListAsync();
        active.Should().ContainSingle(i => i.Name == "Rice");

        var all = await svc.ListAsync(includeInactive: true);
        all.Should().HaveCount(2);
        all.Single(i => i.Name == "Tomato").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_Should_RejectBaseUnitChange_WhenIngredientReferencedByActiveRecipe()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        await SeedActiveRecipeAsync(ctx, tenantId, "Jollof rice", rice.Id);

        // No unit conversion in v1 (§10): the recipe's "1" would silently become 1 g, not 1 kg.
        var act = async () => await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "g"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*base unit*'kg' to 'g'*Jollof rice*no unit conversion*");

        // The stored master data is untouched.
        var stored = (await svc.ListAsync()).Single(i => i.Id == rice.Id);
        stored.BaseUnit.Should().Be("kg");
    }

    [Fact]
    public async Task Update_Should_AllowBaseUnitChange_WhenNoLiveComponentReferencesIngredient()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        var tomato = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));

        // The recipe referenced rice once, but replacing it (R2) soft-deletes that component —
        // only live components of active recipes pin the base unit.
        var variantId = await SeedVariantAsync(ctx, tenantId, "JOLLOF");
        var recipes = new RecipeService(ctx, new TestTenantProvider(tenantId));
        await recipes.SetRecipeAsync(new SetRecipeCommand(variantId, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(rice.Id, 1m),
        }));
        await recipes.SetRecipeAsync(new SetRecipeCommand(variantId, "Jollof rice", 4m, "portion", new[]
        {
            new RecipeComponentCommand(tomato.Id, 0.5m),
        }));

        var updated = await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "g"));

        updated.BaseUnit.Should().Be("g");
    }

    [Fact]
    public async Task Update_Should_RejectBaseUnitChange_WhenCostRowsExist_EvenWithoutRecipes()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        var costs = new IngredientCostService(ctx, new TestTenantProvider(tenantId), new CommerceTestHarness.TestClock());
        await costs.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));

        // Recorded costs (Spec 051) are amounts per the CURRENT base unit and v1 has no unit
        // conversion: with no recipes at all, kg→g would still silently turn ₦1,200/kg into
        // ₦1,200/g at rollup — rejected, with the deactivate-and-recreate path advised.
        var act = async () => await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "g"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*base unit*'kg' to 'g'*recorded costs*no unit conversion*Deactivate*");

        // The stored master data is untouched.
        (await svc.ListAsync()).Single(i => i.Id == rice.Id).BaseUnit.Should().Be("kg");
    }

    [Fact]
    public async Task Update_Should_AllowBaseUnitChange_WhenNeitherRecipesNorCostsReferenceIngredient()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        var tomato = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));

        // A cost on ANOTHER ingredient must not pin rice's unit — the guard is per-ingredient.
        var costs = new IngredientCostService(ctx, new TestTenantProvider(tenantId), new CommerceTestHarness.TestClock());
        await costs.SetCostAsync(new SetIngredientCostCommand(tomato.Id, "NGN", 800m));

        var updated = await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "g"));

        updated.BaseUnit.Should().Be("g");
    }

    [Fact]
    public async Task Create_Should_TreatBlankSkuAsUnset()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        // Two blank SKUs must not collide as duplicates — blank means "no SKU", matching the
        // filtered unique index ([Sku] IS NOT NULL).
        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg", Sku: "   "));
        var tomato = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg", Sku: ""));

        rice.Sku.Should().BeNull();
        tomato.Sku.Should().BeNull();
    }

    [Fact]
    public async Task Update_Should_TreatBlankSkuAsUnset()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg", Sku: "ING-RICE"));
        var tomato = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg", Sku: "ING-TOM"));

        var riceUpdated = await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "kg", Sku: "   "));
        var tomatoUpdated = await svc.UpdateAsync(new UpdateIngredientCommand(tomato.Id, "Tomato", "kg", Sku: ""));

        // Both cleared to null; the second blank does not trip the duplicate-SKU pre-check.
        riceUpdated.Sku.Should().BeNull();
        tomatoUpdated.Sku.Should().BeNull();
    }

    [Fact]
    public async Task Create_Should_TrimNameAndSku()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("  Rice  ", "kg", Sku: " ING-RICE "));

        rice.Name.Should().Be("Rice");
        rice.Sku.Should().Be("ING-RICE");

        // The duplicate-name check sees the trimmed value.
        var act = async () => await svc.CreateAsync(new CreateIngredientCommand("Rice ", "kg"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Rice*already exists*");
    }

    [Fact]
    public async Task Deactivate_Should_Reject_WhenActiveRecipeReferencesIngredient()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        await SeedActiveRecipeAsync(ctx, tenantId, "Jollof rice", rice.Id);

        var act = async () => await svc.DeactivateAsync(rice.Id);

        // The error names the referencing recipe(s) so the operator knows what to update first.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot deactivate*Rice*'Jollof rice'*");

        (await svc.ListAsync()).Single(i => i.Id == rice.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Update_Should_RejectDeactivation_WhenActiveRecipeReferencesIngredient()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        await SeedActiveRecipeAsync(ctx, tenantId, "Jollof rice", rice.Id);

        // An explicit IsActive=false through the update path hits the same guard as DeactivateAsync.
        var act = async () => await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "kg", IsActive: false));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot deactivate*Rice*'Jollof rice'*");

        (await svc.ListAsync()).Single(i => i.Id == rice.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Update_Should_PreserveActiveState_WhenIsActiveOmitted()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        await svc.DeactivateAsync(rice.Id);

        // A master-data touch-up that says nothing about IsActive must not silently reactivate.
        var updated = await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Basmati rice", "kg"));

        updated.Name.Should().Be("Basmati rice");
        updated.IsActive.Should().BeFalse();
        (await svc.ListAsync(includeInactive: true)).Single(i => i.Id == rice.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_Should_Reactivate_WhenIsActiveTrueExplicit()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));
        await svc.DeactivateAsync(rice.Id);

        var updated = await svc.UpdateAsync(new UpdateIngredientCommand(rice.Id, "Rice", "kg", IsActive: true));

        updated.IsActive.Should().BeTrue();
        (await svc.ListAsync()).Should().ContainSingle(i => i.Id == rice.Id);
    }
}
