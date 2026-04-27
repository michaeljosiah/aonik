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

    /// <summary>
    /// Agent-reported confidence in the proposal, on a 0..1 scale.
    /// Defaults to 0.85 for rows written before the column existed (the
    /// migration backfills existing rows from RiskTier).
    /// </summary>
    public decimal Confidence { get; set; } = 0.85m;

    public ProposalStatus Status { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}
