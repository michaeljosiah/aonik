using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record WorkflowGovernanceCommand(
    string Slug,
    string ExpectedVersion,
    string DefinitionHash,
    Guid? ExpectedPublishedVersionId,
    Guid? TargetVersionId = null);

public sealed record WorkflowGovernanceRequestResult(Guid ProposalId, Guid ApprovalRequestId, ToolApprovalPreview Preview);
