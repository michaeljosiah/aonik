using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Catalog;

public class CatalogBillerService : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BillerId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public bool SupportsPartialPayment { get; set; }
    public bool RequiresValidation { get; set; }
    public bool IsActive { get; set; } = true;
    public string FieldsJson { get; set; } = "[]";
    public string? ValidationJson { get; set; }
    public int SortOrder { get; set; }
}
