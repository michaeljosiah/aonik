using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>Ingredient master management over <see cref="CommerceDbContext"/> (Spec 050 §8).</summary>
internal sealed class IngredientService : IIngredientService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public IngredientService(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<IngredientDto> CreateAsync(CreateIngredientCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        ValidateNameAndUnit(command.Name, command.BaseUnit);

        var name = command.Name.Trim();
        var sku = NormalizeSku(command.Sku);

        if (await _dbContext.Ingredients.AnyAsync(i => i.TenantId == tenantId && i.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient named '{name}' already exists.");
        }
        if (sku is not null
            && await _dbContext.Ingredients.AnyAsync(i => i.TenantId == tenantId && i.Sku == sku, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient with SKU '{sku}' already exists.");
        }

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Sku = sku,
            BaseUnit = command.BaseUnit,
            Category = command.Category,
            IsActive = true,
            Notes = command.Notes,
        };

        _dbContext.Ingredients.Add(ingredient);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(ingredient);
    }

    public async Task<IngredientDto> UpdateAsync(UpdateIngredientCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        ValidateNameAndUnit(command.Name, command.BaseUnit);

        var name = command.Name.Trim();
        var sku = NormalizeSku(command.Sku);

        var ingredient = await _dbContext.Ingredients
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId && i.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Ingredient '{command.IngredientId}' was not found.");

        if (await _dbContext.Ingredients.AnyAsync(
                i => i.TenantId == tenantId && i.Id != command.IngredientId && i.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient named '{name}' already exists.");
        }
        if (sku is not null
            && await _dbContext.Ingredients.AnyAsync(
                i => i.TenantId == tenantId && i.Id != command.IngredientId && i.Sku == sku, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient with SKU '{sku}' already exists.");
        }

        // A null IsActive preserves the stored state — an update that says nothing about the
        // flag must never silently reactivate (or deactivate) an ingredient.
        var isActive = command.IsActive ?? ingredient.IsActive;

        var baseUnitChanged = ingredient.BaseUnit != command.BaseUnit;
        var deactivating = ingredient.IsActive && !isActive;
        if (baseUnitChanged || deactivating)
        {
            var referencingRecipes = await GetActiveRecipeNamesReferencingAsync(tenantId, ingredient.Id, cancellationToken);
            if (referencingRecipes.Count > 0)
            {
                if (baseUnitChanged)
                {
                    // Recipe quantities are stored as bare numbers in the ingredient's base unit
                    // and v1 has no unit conversion (§10) — changing the unit under a live recipe
                    // would silently relabel its quantities (e.g. 1 kg becoming 1 g).
                    throw new InvalidOperationException(
                        $"Cannot change the base unit of ingredient '{ingredient.Name}' from '{ingredient.BaseUnit}' to " +
                        $"'{command.BaseUnit}' while active recipes reference it ({FormatNames(referencingRecipes)}). " +
                        "There is no unit conversion in v1, so recipe quantities would be silently relabeled " +
                        "(e.g. 1 kg becoming 1 g). Update those recipes first.");
                }
                throw DeactivationBlocked(ingredient.Name, referencingRecipes);
            }

            if (baseUnitChanged && await HasLiveCostRowsAsync(tenantId, ingredient.Id, cancellationToken))
            {
                // Recorded costs (Spec 051) are bare amounts per the ingredient's CURRENT base
                // unit and v1 has no unit conversion — changing the unit would silently reprice
                // every stored cost at rollup. This guard IS the v1 answer (no unit snapshot on
                // cost rows); deactivation stays allowed so the advised path works.
                throw new InvalidOperationException(
                    $"Cannot change the base unit of ingredient '{ingredient.Name}' from '{ingredient.BaseUnit}' to " +
                    $"'{command.BaseUnit}' while recorded costs exist for it. Costs are amounts per the current " +
                    "base unit and there is no unit conversion in v1, so they would be silently repriced " +
                    "(e.g. ₦1,200/kg becoming ₦1,200/g). Deactivate this ingredient and create a new one instead.");
            }
        }

        ingredient.Name = name;
        ingredient.Sku = sku;
        ingredient.BaseUnit = command.BaseUnit;
        ingredient.Category = command.Category;
        ingredient.Notes = command.Notes;
        ingredient.IsActive = isActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(ingredient);
    }

    public async Task<IReadOnlyList<IngredientDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _dbContext.Ingredients.AsNoTracking().Where(i => i.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        var ingredients = await query.OrderBy(i => i.Name).ToListAsync(cancellationToken);
        return ingredients.Select(Map).ToList();
    }

    public async Task DeactivateAsync(Guid ingredientId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var ingredient = await _dbContext.Ingredients
            .FirstOrDefaultAsync(i => i.Id == ingredientId && i.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Ingredient '{ingredientId}' was not found.");

        var referencingRecipes = await GetActiveRecipeNamesReferencingAsync(tenantId, ingredient.Id, cancellationToken);
        if (referencingRecipes.Count > 0)
        {
            throw DeactivationBlocked(ingredient.Name, referencingRecipes);
        }

        ingredient.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateNameAndUnit(string name, string baseUnit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Ingredient name is required.");
        }
        if (string.IsNullOrWhiteSpace(baseUnit))
        {
            throw new ArgumentException("Ingredient base unit is required (e.g. kg, g, L, ml, each).");
        }
    }

    /// <summary>A blank SKU means "no SKU": trim, and normalize null/empty/whitespace to null so
    /// the filtered unique index ([Sku] IS NOT NULL) only bites when a SKU is genuinely set.</summary>
    private static string? NormalizeSku(string? sku)
    {
        var trimmed = sku?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Names of ACTIVE recipes with a live (non-soft-deleted) component referencing the
    /// ingredient — the references that make a base-unit change or deactivation unsafe.</summary>
    private async Task<IReadOnlyList<string>> GetActiveRecipeNamesReferencingAsync(
        Guid tenantId, Guid ingredientId, CancellationToken cancellationToken)
        => await _dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive
                && r.Components.Any(c => c.IngredientId == ingredientId && !c.IsDeleted))
            .Select(r => r.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

    /// <summary>Whether any live (non-soft-deleted) <see cref="Entities.Sourcing.IngredientCost"/>
    /// rows (Spec 051) exist for the ingredient, in any currency. Recorded costs are amounts per
    /// the ingredient's current base unit, so they pin it — v1 has no unit conversion.</summary>
    private async Task<bool> HasLiveCostRowsAsync(Guid tenantId, Guid ingredientId, CancellationToken cancellationToken)
        => await _dbContext.IngredientCosts
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.IngredientId == ingredientId && !c.IsDeleted, cancellationToken);

    private static InvalidOperationException DeactivationBlocked(string ingredientName, IReadOnlyList<string> recipeNames)
        => new(
            $"Cannot deactivate ingredient '{ingredientName}' while active recipes reference it: " +
            $"{FormatNames(recipeNames)}. Update those recipes first.");

    private static string FormatNames(IEnumerable<string> names)
        => string.Join(", ", names.Select(n => $"'{n}'"));

    private static IngredientDto Map(Ingredient i)
        => new(i.Id, i.Name, i.Sku, i.BaseUnit, i.Category, i.IsActive, i.Notes);
}
