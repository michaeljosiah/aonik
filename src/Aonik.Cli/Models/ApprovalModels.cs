namespace Aonik.Cli.Models;

public enum FinancialLifeGraphProposalStatus
{
    Proposed = 1,
    Approved = 2,
    Rejected = 3
}

public sealed record PendingFinancialLifeGraphProposalResponse(
    Guid ProposalId,
    Guid GraphNodeId,
    Guid GraphEdgeId,
    string NodeType,
    string DisplayName,
    string Predicate,
    FinancialLifeGraphProposalStatus Status,
    Guid AiRunId,
    string MetadataJson);

public sealed record RejectFinancialLifeGraphProposalRequest(string? Reason);
