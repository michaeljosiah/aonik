using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiPolicy : AuditableEntity
{
    public Guid AiPolicyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string AllowedDataFieldsJson { get; private set; } = string.Empty;
    public string RedactionRulesJson { get; private set; } = string.Empty;
    public string BannedActionsJson { get; private set; } = string.Empty;
    public string EscalationRulesJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private AiPolicy() { }

    public AiPolicy(string name)
    {
        AiPolicyId = Id;
        Name = name;
        AllowedDataFieldsJson = "[]";
        RedactionRulesJson = "[]";
        BannedActionsJson = "[]";
        EscalationRulesJson = "[]";
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateAllowedDataFields(string allowedDataFieldsJson)
    {
        AllowedDataFieldsJson = allowedDataFieldsJson;
    }

    public void UpdateRedactionRules(string redactionRulesJson)
    {
        RedactionRulesJson = redactionRulesJson;
    }

    public void UpdateBannedActions(string bannedActionsJson)
    {
        BannedActionsJson = bannedActionsJson;
    }

    public void UpdateEscalationRules(string escalationRulesJson)
    {
        EscalationRulesJson = escalationRulesJson;
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
