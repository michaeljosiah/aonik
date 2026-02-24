using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Catalog;

public class CatalogBiller : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid CorrespondentPartnerId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? SupportPhone { get; set; }
    public string? SupportEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
}
