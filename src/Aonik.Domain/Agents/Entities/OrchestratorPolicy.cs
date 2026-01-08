using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Agents.Entities;

public class OrchestratorPolicy : AuditableEntity
{
    public Guid OrchestratorPolicyId { get; set; }
    public Guid? TenantId { get; set; }
    public string IntentType { get; set; } = string.Empty;
    public string PreferredAgentsJson { get; set; } = string.Empty;
    public string FallbackAgentsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
