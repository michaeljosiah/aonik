using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Pricing;

public class FxRateSource : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int RefreshIntervalMinutes { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    public bool IsActive { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
