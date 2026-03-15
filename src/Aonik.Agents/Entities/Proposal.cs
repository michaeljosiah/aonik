using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

public class Proposal : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ProposalType { get; set; } = string.Empty;
    public Guid ProposedByAgentId { get; set; }
    public Guid AiRunId { get; set; }
    public string ImpactSummary { get; set; } = string.Empty;
    public string RiskTier { get; set; } = string.Empty;
    public ProposalStatus Status { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}
