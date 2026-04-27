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
