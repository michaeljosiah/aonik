using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>A catalog category, optionally nested (Spec 042 §8). Anemic.</summary>
public class ProductCategory : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
}
