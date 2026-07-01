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

        if (await _dbContext.Ingredients.AnyAsync(i => i.TenantId == tenantId && i.Name == command.Name, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient named '{command.Name}' already exists.");
        }
        if (command.Sku is not null
            && await _dbContext.Ingredients.AnyAsync(i => i.TenantId == tenantId && i.Sku == command.Sku, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient with SKU '{command.Sku}' already exists.");
        }

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = command.Name,
            Sku = command.Sku,
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

        var ingredient = await _dbContext.Ingredients
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId && i.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Ingredient '{command.IngredientId}' was not found.");

        if (await _dbContext.Ingredients.AnyAsync(
                i => i.TenantId == tenantId && i.Id != command.IngredientId && i.Name == command.Name, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient named '{command.Name}' already exists.");
        }
        if (command.Sku is not null
            && await _dbContext.Ingredients.AnyAsync(
                i => i.TenantId == tenantId && i.Id != command.IngredientId && i.Sku == command.Sku, cancellationToken))
        {
            throw new InvalidOperationException($"An ingredient with SKU '{command.Sku}' already exists.");
        }

        ingredient.Name = command.Name;
        ingredient.Sku = command.Sku;
        ingredient.BaseUnit = command.BaseUnit;
        ingredient.Category = command.Category;
        ingredient.Notes = command.Notes;
        ingredient.IsActive = command.IsActive;

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

    private static IngredientDto Map(Ingredient i)
        => new(i.Id, i.Name, i.Sku, i.BaseUnit, i.Category, i.IsActive, i.Notes);
}
