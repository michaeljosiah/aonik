using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Features;

public class TenantFeature : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string FeatureName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
}
