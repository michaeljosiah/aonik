using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Production;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Production;

/// <summary>Recipe / bill-of-materials management + explosion over <see cref="CommerceDbContext"/> (Spec 050 §8/§11).</summary>
internal sealed class RecipeService : IRecipeService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public RecipeService(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<RecipeDto> SetRecipeAsync(SetRecipeCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException("Recipe name is required.");
        }
        if (string.IsNullOrWhiteSpace(command.YieldUnit))
        {
            throw new ArgumentException("Recipe yield unit is required (e.g. portion).");
        }
        if (command.YieldQuantity <= 0m)
        {
            throw new ArgumentException("Recipe yield quantity must be positive.");
        }
        if (command.Components is not { Count: > 0 })
        {
            throw new ArgumentException("A recipe requires at least one component.");
        }
        foreach (var line in command.Components)
        {
            if (line.Quantity <= 0m)
            {
                throw new ArgumentException($"Component quantity for ingredient '{line.IngredientId}' must be positive.");
            }
        }

        // Duplicate ingredient entries in one command are merged (§8): quantities summed, first
        // non-empty note kept.
        var components = command.Components
            .GroupBy(c => c.IngredientId)
            .Select(g => new RecipeComponentCommand(
                g.Key,
                g.Sum(c => c.Quantity),
                g.Select(c => c.Notes).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))))
            .ToList();

        var variantExists = await _dbContext.ProductVariants
            .AnyAsync(v => v.Id == command.ProductVariantId && v.TenantId == tenantId, cancellationToken);
        if (!variantExists)
        {
            throw new InvalidOperationException($"Product variant '{command.ProductVariantId}' was not found.");
        }

        var ingredientIds = components.Select(c => c.IngredientId).ToList();
        var ingredients = await _dbContext.Ingredients
            .Where(i => i.TenantId == tenantId && ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        foreach (var line in components)
        {
            if (!ingredients.TryGetValue(line.IngredientId, out var ingredient))
            {
                throw new InvalidOperationException($"Ingredient '{line.IngredientId}' was not found.");
            }
            if (!ingredient.IsActive)
            {
                throw new InvalidOperationException(
                    $"Ingredient '{ingredient.Name}' is deactivated and cannot be used in a recipe.");
            }
        }

        // Replace in place (R2): overwrite the existing active recipe's name/yield and its
        // component rows under the same audited entity — never insert a second active recipe
        // (R3; the filtered unique index guards SQL Server under concurrency).
        var recipe = await _dbContext.Recipes
            .Include(r => r.Components)
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.ProductVariantId == command.ProductVariantId && r.IsActive,
                cancellationToken);

        if (recipe is null)
        {
            recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductVariantId = command.ProductVariantId,
                IsActive = true,
            };
            _dbContext.Recipes.Add(recipe);
        }
        else
        {
            _dbContext.RecipeComponents.RemoveRange(recipe.Components);
            recipe.Components.Clear();
        }

        recipe.Name = command.Name;
        recipe.YieldQuantity = command.YieldQuantity;
        recipe.YieldUnit = command.YieldUnit;

        foreach (var line in components)
        {
            var component = new RecipeComponent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RecipeId = recipe.Id,
                IngredientId = line.IngredientId,
                Quantity = line.Quantity,
                Notes = line.Notes,
            };
            recipe.Components.Add(component);
            // Explicit Add: a component discovered only via the navigation of an existing
            // (non-Added) recipe would be attached as Modified, not Added, in the replace path.
            _dbContext.RecipeComponents.Add(component);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(recipe, ingredients);
    }

    public async Task<RecipeDto?> GetRecipeAsync(Guid productVariantId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var recipe = await _dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Components)
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.ProductVariantId == productVariantId && r.IsActive,
                cancellationToken);
        if (recipe is null)
        {
            return null;
        }

        var ingredients = await LoadIngredientsAsync(
            tenantId, recipe.Components.Select(c => c.IngredientId), cancellationToken);
        return Map(recipe, ingredients);
    }

    public async Task<RecipeExplosionDto> ExplodeAsync(Guid productVariantId, decimal portions, CancellationToken cancellationToken = default)
    {
        if (portions <= 0m)
        {
            throw new ArgumentException("Portions must be positive.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var recipe = await _dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Components)
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.ProductVariantId == productVariantId && r.IsActive,
                cancellationToken);

        // No active recipe explodes to an empty bill with a diagnostic flag — never a silent
        // zero (R5).
        if (recipe is null)
        {
            return new RecipeExplosionDto(productVariantId, portions, HasActiveRecipe: false, Array.Empty<ExplodedLineDto>());
        }

        var ingredients = await LoadIngredientsAsync(
            tenantId, recipe.Components.Select(c => c.IngredientId), cancellationToken);

        // Scale each component by portions / YieldQuantity (§11/R4).
        var factor = portions / recipe.YieldQuantity;
        var lines = recipe.Components
            .Select(c => MapLine(c.IngredientId, c.Quantity * factor, ingredients))
            .OrderBy(l => l.IngredientName, StringComparer.Ordinal)
            .ToList();

        return new RecipeExplosionDto(productVariantId, portions, HasActiveRecipe: true, lines);
    }

    public async Task<BillOfMaterialsDto> ExplodeManyAsync(IReadOnlyList<VariantDemand> demands, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (demands is not { Count: > 0 })
        {
            return new BillOfMaterialsDto(Array.Empty<ExplodedLineDto>(), Array.Empty<Guid>());
        }
        foreach (var demand in demands)
        {
            if (demand.Portions <= 0m)
            {
                throw new ArgumentException($"Portions for variant '{demand.ProductVariantId}' must be positive.");
            }
        }

        var variantIds = demands.Select(d => d.ProductVariantId).Distinct().ToList();
        var recipes = await _dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Components)
            .Where(r => r.TenantId == tenantId && r.IsActive && variantIds.Contains(r.ProductVariantId))
            .ToListAsync(cancellationToken);
        var recipesByVariant = recipes.ToDictionary(r => r.ProductVariantId);

        // Explode each demand and sum RequiredQuantity per ingredient (§11/R4); variants without
        // an active recipe are reported, never silently under-counted (R5).
        var requiredByIngredient = new Dictionary<Guid, decimal>();
        var variantsWithoutRecipe = new HashSet<Guid>();

        foreach (var demand in demands)
        {
            if (!recipesByVariant.TryGetValue(demand.ProductVariantId, out var recipe))
            {
                variantsWithoutRecipe.Add(demand.ProductVariantId);
                continue;
            }

            var factor = demand.Portions / recipe.YieldQuantity;
            foreach (var component in recipe.Components)
            {
                requiredByIngredient[component.IngredientId] =
                    requiredByIngredient.GetValueOrDefault(component.IngredientId) + component.Quantity * factor;
            }
        }

        var ingredients = await LoadIngredientsAsync(tenantId, requiredByIngredient.Keys, cancellationToken);
        var lines = requiredByIngredient
            .Select(kvp => MapLine(kvp.Key, kvp.Value, ingredients))
            .OrderBy(l => l.IngredientName, StringComparer.Ordinal)
            .ToList();

        return new BillOfMaterialsDto(lines, variantsWithoutRecipe.ToList());
    }

    private async Task<Dictionary<Guid, Ingredient>> LoadIngredientsAsync(
        Guid tenantId, IEnumerable<Guid> ingredientIds, CancellationToken cancellationToken)
    {
        var ids = ingredientIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Ingredient>();
        }

        return await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
    }

    private static ExplodedLineDto MapLine(
        Guid ingredientId, decimal requiredQuantity, IReadOnlyDictionary<Guid, Ingredient> ingredients)
    {
        ingredients.TryGetValue(ingredientId, out var ingredient);
        return new ExplodedLineDto(
            ingredientId,
            ingredient?.Name ?? string.Empty,
            ingredient?.BaseUnit ?? string.Empty,
            requiredQuantity);
    }

    private static RecipeDto Map(Recipe recipe, IReadOnlyDictionary<Guid, Ingredient> ingredients)
    {
        var components = recipe.Components
            .Select(c =>
            {
                ingredients.TryGetValue(c.IngredientId, out var ingredient);
                return new RecipeComponentDto(
                    c.Id,
                    c.IngredientId,
                    ingredient?.Name ?? string.Empty,
                    ingredient?.BaseUnit ?? string.Empty,
                    c.Quantity,
                    c.Notes);
            })
            .OrderBy(c => c.IngredientName, StringComparer.Ordinal)
            .ToList();

        return new RecipeDto(
            recipe.Id,
            recipe.ProductVariantId,
            recipe.Name,
            recipe.YieldQuantity,
            recipe.YieldUnit,
            recipe.IsActive,
            components);
    }
}
