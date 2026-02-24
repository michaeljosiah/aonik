using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Catalog;

public class CatalogBillerCategory : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
