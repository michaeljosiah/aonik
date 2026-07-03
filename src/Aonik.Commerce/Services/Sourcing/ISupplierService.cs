using Aonik.Commerce.Contracts.Models.Sourcing;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Supplier master + supplier catalog (price list) management for the Commerce module
/// (Spec 053 §9). A supplier is a light sourcing-side counterparty — not a full party/KYC record;
/// <c>PartyId</c> optionally soft-links a platform Party. The catalog rows carry the buy-side
/// pack→base-unit conversion (<c>PackSize</c>) and pack price the purchase-order paths default from.
/// </summary>
public interface ISupplierService
{
    /// <summary>Creates a supplier. Name must be unique per tenant.</summary>
    Task<SupplierDto> CreateAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default);

    /// <summary>Updates a supplier's master data. A null <c>IsActive</c> preserves the stored state.</summary>
    Task<SupplierDto> UpdateAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default);

    /// <summary>The supplier by id; null when not found.</summary>
    Task<SupplierDto?> GetAsync(Guid supplierId, CancellationToken cancellationToken = default);

    /// <summary>Lists the tenant's suppliers ordered by name; active only unless <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts one price-list row keyed by (supplier, ingredient): creates it, or updates the
    /// existing row's pack/price/SKU/lead time. Requires an active supplier and an active
    /// ingredient; <c>PackSize</c> and <c>PackPrice</c> must be positive.
    /// </summary>
    Task<SupplierIngredientDto> UpsertCatalogItemAsync(UpsertSupplierIngredientCommand command, CancellationToken cancellationToken = default);

    /// <summary>The supplier's catalog rows, ordered by ingredient name.</summary>
    Task<IReadOnlyList<SupplierIngredientDto>> ListCatalogAsync(Guid supplierId, CancellationToken cancellationToken = default);

    /// <summary>"Who supplies this ingredient?" — every supplier's catalog row for it, cheapest per base unit first.</summary>
    Task<IReadOnlyList<SupplierIngredientDto>> ListSuppliersForIngredientAsync(Guid ingredientId, CancellationToken cancellationToken = default);
}
