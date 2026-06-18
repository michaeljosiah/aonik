using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A purchasable variant of a <see cref="Product"/> (Spec 042 §8). The <see cref="Sku"/> and
/// <see cref="Id"/> are the values copied onto an order line's <c>Sku</c> / soft <c>ProductId</c>
/// at checkout — the catalog is the source of truth; the order keeps an immutable snapshot.
/// </summary>
public class ProductVariant : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON of variant options, e.g. {"size":"500g","flavour":"vanilla"}.</summary>
    public string OptionsJson { get; set; } = "{}";

    public decimal? WeightGrams { get; set; }
    public bool IsActive { get; set; } = true;

    public List<ProductPrice> Prices { get; set; } = new();
}
