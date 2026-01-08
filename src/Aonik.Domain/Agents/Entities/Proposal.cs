using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Agents.Entities;

public class Proposal : AuditableEntity, ITenantScoped
{
    public Guid ProposalId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProposalType { get; private set; } = string.Empty;
    public Guid ProposedByAgentId { get; private set; }
    public Guid AiRunId { get; private set; }
    public string ImpactSummary { get; private set; } = string.Empty;
    public string RiskTier { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;

    private Proposal() { }

    public Proposal(Guid tenantId, string proposalType, Guid proposedByAgentId, Guid aiRunId, string impactSummary, string riskTier, string payloadJson)
    {
        ProposalId = Id;
        TenantId = tenantId;
        ProposalType = proposalType;
        ProposedByAgentId = proposedByAgentId;
        AiRunId = aiRunId;
        ImpactSummary = impactSummary;
        RiskTier = riskTier;
        PayloadJson = payloadJson;
        Status = "Pending";
    }

    public void Approve(Guid userId)
    {
        Status = "Approved";
        ApprovedByUserId = userId;
        ApprovedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        Status = "Rejected";
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }
}
