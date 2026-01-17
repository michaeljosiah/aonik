using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiRoutePolicy : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public string RiskTier { get; set; } = string.Empty;
    public string DataSensitivity { get; set; } = string.Empty;
    public decimal CostCeiling { get; set; }
    public Guid PrimaryModelId { get; set; }
    public string FallbackModelIdsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
