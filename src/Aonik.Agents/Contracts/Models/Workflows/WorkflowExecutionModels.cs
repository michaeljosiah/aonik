using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record StartWorkflowRunRequest(Guid WorkflowVersionId, string SubjectType, Guid SubjectId, string IdempotencyKey, string? ExpectedFactsHash = null);
public sealed record WorkflowRunDetail(
    Guid Id, Guid WorkflowId, Guid? WorkflowVersionId, string? DefinitionHash, string Status,
    string? Outcome, string? ErrorCode, string? ActorType, Guid? ActorId, Guid? InitiatingUserId,
    string? SubjectType, Guid? SubjectId, string ExpectedVersion,
    DateTime StartedAt, DateTime? CompletedAt, IReadOnlyList<WorkflowStepDetail> Steps, Guid? ConfiguredByUserId = null);
public sealed record WorkflowStepDetail(Guid Id, string NodeKey, string InvocationPath, string Status,
    string? ActionKey, int? ActionVersion, string? SelectedRoute, JsonElement? DecisionTrace, JsonElement? Result,
    Guid? ApprovalRequestId, Guid? ProposalId, Guid? AiRunId, string? ErrorCode, WorkflowStepApproval? Approval = null);
public sealed record WorkflowStepApproval(Guid Id, string RiskTier, string ActionKind, string Status, DateTime ExpiresAt,
    bool CanDecide, ToolApprovalPreview? Preview);
public sealed record WorkflowRunCommand(string ExpectedVersion);
public sealed record WorkflowToolParameters(
    string ActionKey, int ContractVersion, IReadOnlyDictionary<string, WorkflowInputBinding> Inputs,
    WorkflowReviewerRequirement? Reviewer = null, WorkflowConnectorBinding? Connection = null);
public sealed record WorkflowInputBinding(JsonElement? Literal = null, string? Fact = null, string? Step = null, string? Output = null);
public sealed record WorkflowReviewerRequirement(Guid RoleId, bool ForbidSelfApproval = true);
