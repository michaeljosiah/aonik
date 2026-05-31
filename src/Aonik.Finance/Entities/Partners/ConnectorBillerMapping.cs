using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// Maps a logical CatalogBiller (plus an optional service) to a connector's provider codes, so the
/// same biller routes to multiple partners. Provider-specific biller / item codes live ONLY here
/// (<see cref="ProviderBillerCode"/> / <see cref="ProviderItemCode"/>) - CatalogBillerService.ServiceCode
/// stays the logical AONIK code, never a vendor code. Telcos are billers too, so airtime / data
/// routing flows through the same mapping. The core multi-partner enabler. Tenant-scoped.
/// </summary>
public class ConnectorBillerMapping : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CatalogBillerId { get; set; }
    public Guid? CatalogBillerServiceId { get; set; }
    public Guid ConnectorId { get; set; }
    public string ProviderBillerCode { get; set; } = string.Empty;
    public string? ProviderItemCode { get; set; }
    public bool IsActive { get; set; } = true;
}
