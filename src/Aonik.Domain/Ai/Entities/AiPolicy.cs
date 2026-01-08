using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiPolicy : AuditableEntity
{
    public Guid AiPolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AllowedDataFieldsJson { get; set; } = string.Empty;
    public string RedactionRulesJson { get; set; } = string.Empty;
    public string BannedActionsJson { get; set; } = string.Empty;
    public string EscalationRulesJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
