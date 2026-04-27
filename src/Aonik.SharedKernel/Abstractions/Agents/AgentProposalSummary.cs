namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Read-only projection of an agent <c>Proposal</c> joined with its parent
/// <c>Agent</c> for cross-module consumption (dashboards, queues, audit views).
/// Lives in SharedKernel so non-Agents modules can render proposals without
/// taking a project reference on Aonik.Agents.
/// </summary>
/// <param name="Id">Proposal id.</param>
/// <param name="AgentName">Display name of the proposing agent.</param>
/// <param name="AgentDomain">Domain group (e.g. "Billing", "Payout").</param>
/// <param name="AgentIconUrl">Optional avatar URL for the agent.</param>
/// <param name="Confidence">Derived from <c>RiskTier</c> (Low=0.95 / Medium=0.85 / High=0.70 / unknown=0.80) until a real confidence column lands on Proposal.</param>
/// <param name="Summary">Human-readable summary (sourced from <c>Proposal.ImpactSummary</c>).</param>
/// <param name="Reason">Free-form rationale; null until a Reason column lands on Proposal.</param>
/// <param name="RiskTier">Risk classification — drives confidence and visual treatment.</param>
/// <param name="CreatedAt">When the proposal was raised (UTC).</param>
public record AgentProposalSummary(
    Guid Id,
    string AgentName,
    string AgentDomain,
    string? AgentIconUrl,
    decimal Confidence,
    string Summary,
    string? Reason,
    string RiskTier,
    DateTime CreatedAt);
