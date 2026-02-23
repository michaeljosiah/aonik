using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Pricing;

public class FxRefreshSchedule : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
