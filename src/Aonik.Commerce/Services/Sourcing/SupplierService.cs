using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>Supplier master + catalog management over <see cref="CommerceDbContext"/> (Spec 053 §9).</summary>
internal sealed class SupplierService : ISupplierService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public SupplierService(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var name = ValidateName(command.Name);
        var currency = NormalizeCurrency(command.Currency);
        ValidateLeadTime(command.LeadTimeDays);

        if (await _dbContext.Suppliers.AnyAsync(s => s.TenantId == tenantId && s.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"A supplier named '{name}' already exists.");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = NormalizePartyId(command.PartyId),
            Name = name,
            Currency = currency,
            LeadTimeDays = command.LeadTimeDays,
            PaymentTerms = NormalizeOptional(command.PaymentTerms),
            IsActive = true,
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var name = ValidateName(command.Name);
        var currency = NormalizeCurrency(command.Currency);
        ValidateLeadTime(command.LeadTimeDays);

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == command.SupplierId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier '{command.SupplierId}' was not found.");

        if (await _dbContext.Suppliers.AnyAsync(
                s => s.TenantId == tenantId && s.Id != command.SupplierId && s.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"A supplier named '{name}' already exists.");
        }

        supplier.Name = name;
        supplier.Currency = currency;
        supplier.PartyId = NormalizePartyId(command.PartyId);
        supplier.LeadTimeDays = command.LeadTimeDays;
        supplier.PaymentTerms = NormalizeOptional(command.PaymentTerms);
        // A null IsActive preserves the stored state — an update that says nothing about the flag
        // must never silently reactivate (or deactivate) a supplier.
        supplier.IsActive = command.IsActive ?? supplier.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(supplier);
    }

    public async Task<SupplierDto?> GetAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.TenantId == tenantId, cancellationToken);
        return supplier is null ? null : Map(supplier);
    }

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _dbContext.Suppliers.AsNoTracking().Where(s => s.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var suppliers = await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return suppliers.Select(Map).ToList();
    }

    public async Task<SupplierIngredientDto> UpsertCatalogItemAsync(UpsertSupplierIngredientCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (command.PackSize <= 0)
        {
            throw new ArgumentException("PackSize must be positive — it is the quantity of the ingredient's base unit one pack contains.");
        }
        if (command.PackPrice <= 0)
        {
            throw new ArgumentException("PackPrice must be positive.");
        }

        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SupplierId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier '{command.SupplierId}' was not found.");
        if (!supplier.IsActive)
        {
            throw new InvalidOperationException($"Supplier '{supplier.Name}' is inactive; reactivate it before editing its catalog.");
        }

        var ingredient = await _dbContext.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId && i.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Ingredient '{command.IngredientId}' was not found.");
        if (!ingredient.IsActive)
        {
            throw new InvalidOperationException($"Ingredient '{ingredient.Name}' is inactive and cannot be added to a supplier catalog.");
        }

        var currency = NormalizeCurrency(command.Currency ?? supplier.Currency);
        var sku = NormalizeOptional(command.Sku);

        var row = await _dbContext.SupplierIngredients
            .FirstOrDefaultAsync(
                si => si.TenantId == tenantId && si.SupplierId == command.SupplierId && si.IngredientId == command.IngredientId,
                cancellationToken);
        if (row is null)
        {
            row = new SupplierIngredient
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SupplierId = command.SupplierId,
                IngredientId = command.IngredientId,
            };
            _dbContext.SupplierIngredients.Add(row);
        }

        row.Sku = sku;
        row.PackSize = command.PackSize;
        row.PackPrice = command.PackPrice;
        row.Currency = currency;
        row.LeadTimeDays = command.LeadTimeDays;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(row, supplier.Name, ingredient.Name, ingredient.BaseUnit);
    }

    public async Task<IReadOnlyList<SupplierIngredientDto>> ListCatalogAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryCatalogAsync(si => si.SupplierId == supplierId, cancellationToken);
        return rows.OrderBy(r => r.IngredientName).ToList();
    }

    public async Task<IReadOnlyList<SupplierIngredientDto>> ListSuppliersForIngredientAsync(Guid ingredientId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryCatalogAsync(si => si.IngredientId == ingredientId, cancellationToken);
        return rows.OrderBy(r => r.UnitPrice).ToList();
    }

    /// <summary>Catalog rows matching <paramref name="predicate"/>, left-joined to supplier and
    /// ingredient names for readability — the LowStockAlertService GroupJoin pattern. Projected to
    /// an anonymous shape in-query (a named-record constructor projection does not translate) and
    /// mapped to DTOs in memory; callers apply their own ordering.</summary>
    private async Task<List<SupplierIngredientDto>> QueryCatalogAsync(
        System.Linq.Expressions.Expression<Func<SupplierIngredient, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var rows = await _dbContext.SupplierIngredients.AsNoTracking()
            .Where(si => si.TenantId == tenantId)
            .Where(predicate)
            .GroupJoin(
                _dbContext.Suppliers.AsNoTracking().Where(s => s.TenantId == tenantId),
                si => si.SupplierId,
                s => s.Id,
                (si, suppliers) => new { si, suppliers })
            .SelectMany(
                x => x.suppliers.DefaultIfEmpty(),
                (x, supplier) => new { x.si, SupplierName = supplier != null ? supplier.Name : null })
            .GroupJoin(
                _dbContext.Ingredients.AsNoTracking().Where(i => i.TenantId == tenantId),
                x => x.si.IngredientId,
                i => i.Id,
                (x, ingredients) => new { x.si, x.SupplierName, ingredients })
            .SelectMany(
                x => x.ingredients.DefaultIfEmpty(),
                (x, ingredient) => new
                {
                    Row = x.si,
                    x.SupplierName,
                    IngredientName = ingredient != null ? ingredient.Name : null,
                    IngredientBaseUnit = ingredient != null ? ingredient.BaseUnit : null,
                })
            .ToListAsync(cancellationToken);

        return rows.Select(x => Map(x.Row, x.SupplierName, x.IngredientName, x.IngredientBaseUnit)).ToList();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Supplier name is required.");
        }
        return name.Trim();
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required (ISO 4217, e.g. NGN).");
        }
        return currency.Trim().ToUpperInvariant();
    }

    private static void ValidateLeadTime(int? leadTimeDays)
    {
        if (leadTimeDays is < 0)
        {
            throw new ArgumentException("Lead time cannot be negative.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Guid.Empty means "not party-linked", never a real Party — normalized to null on
    /// create AND update (the Guid twin of the blank-SKU normalization above). A stored empty
    /// PartyId would make BuildSupplierRole emit an empty-party Supplier role, which the spine
    /// rejects — poisoning every subsequent PO create for the supplier.</summary>
    private static Guid? NormalizePartyId(Guid? partyId)
        => partyId == Guid.Empty ? null : partyId;

    private static SupplierDto Map(Supplier s)
        => new(s.Id, s.Name, s.Currency, s.PartyId, s.LeadTimeDays, s.PaymentTerms, s.IsActive);

    private static SupplierIngredientDto Map(SupplierIngredient si, string? supplierName, string? ingredientName, string? baseUnit)
        => new(
            si.Id, si.SupplierId, supplierName, si.IngredientId, ingredientName, baseUnit,
            si.Sku, si.PackSize, si.PackPrice, si.PackPrice / si.PackSize, si.Currency, si.LeadTimeDays);
}
