namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Detail view of a single agent <c>Proposal</c> with its parent agent's
/// display metadata and the JSON payload for inspection in the Review
/// dialog. Status is serialised as a string so the wire shape is stable
/// even if the enum is reordered.
/// </summary>
public sealed record ProposalDetailResponse(
    Guid Id,
    string ProposalType,
    Guid ProposedByAgentId,
    string AgentName,
    string AgentDomain,
    string? AgentIconUrl,
    Guid AiRunId,
    string Summary,
    string RiskTier,
    decimal Confidence,
    string Status,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    string PayloadJson,
    DateTime CreatedAt);

/// <summary>
/// Compact list-row view of a pending agent proposal, returned by the
/// Approvals queue endpoint. Excludes <c>PayloadJson</c> to keep list
/// responses small — call <c>GET /ai/proposals/{id}</c> for the full
/// detail when the user opens a row.
/// </summary>
public sealed record ProposalListItem(
    Guid Id,
    string ProposalType,
    string AgentName,
    string AgentDomain,
    string? AgentIconUrl,
    decimal Confidence,
    string Summary,
    string RiskTier,
    DateTime CreatedAt);

/// <summary>Filter and shape for the proposals queue.</summary>
public sealed record ListProposalsRequest(
    string? ProposalType = null,
    string? AgentDomain = null,
    string? RiskTier = null,
    int Take = 100);

public sealed record ListProposalsResponse(
    IReadOnlyList<ProposalListItem> Items,
    int Total);
