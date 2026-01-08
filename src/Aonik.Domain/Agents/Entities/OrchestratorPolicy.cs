using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Agents.Entities;

public class OrchestratorPolicy : AuditableEntity
{
    public Guid OrchestratorPolicyId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string IntentType { get; private set; } = string.Empty;
    public string PreferredAgentsJson { get; private set; } = string.Empty;
    public string FallbackAgentsJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private OrchestratorPolicy() { }

    public OrchestratorPolicy(string intentType, Guid? tenantId = null)
    {
        OrchestratorPolicyId = Id;
        TenantId = tenantId;
        IntentType = intentType;
        PreferredAgentsJson = "[]";
        FallbackAgentsJson = "[]";
        IsActive = true;
    }

    public void UpdatePreferredAgents(string preferredAgentsJson)
    {
        PreferredAgentsJson = preferredAgentsJson;
    }

    public void UpdateFallbackAgents(string fallbackAgentsJson)
    {
        FallbackAgentsJson = fallbackAgentsJson;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
