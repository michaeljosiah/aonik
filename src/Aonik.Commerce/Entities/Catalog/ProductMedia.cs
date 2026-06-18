using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>An image or document attached to a <see cref="Product"/> (Spec 042 §8). Anemic.</summary>
public class ProductMedia : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;

    /// <summary>image | doc.</summary>
    public string Kind { get; set; } = "image";
    public int SortOrder { get; set; }
}
