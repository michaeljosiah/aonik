using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.SharedKernel.Abstractions.Workflows;

/// <summary>Reviewed domain/connector action used by workflow Tool nodes. The runner enforces its declared gate.</summary>
public interface IWorkflowActionHandler
{
    WorkflowActionDescriptor Descriptor { get; }
    void ValidateArguments(JsonElement arguments);
    Task<ToolApprovalPreview?> GetPreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => Task.FromResult<ToolApprovalPreview?>(null);
    Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default);
    Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default);
}

public sealed record WorkflowActionDescriptor(
    string Key, int Version, string ToolName, string RequiredPermission, ToolApprovalOptions? Approval,
    JsonElement InputSchema, JsonElement OutputSchema, IReadOnlyList<string>? AllowedSubjectBlockReasons = null,
    bool SupportsProposalReconciliation = false);
public sealed record WorkflowActionInvocation(
    Guid RunId, Guid StepRunId, Guid OperationId, WorkflowActor Actor,
    string SubjectType, Guid SubjectId, string ExpectedSubjectVersion, string FactsHash,
    JsonElement Arguments, Guid? ApprovalRequestId = null);
public sealed record WorkflowActionResult(string Outcome, JsonElement Output, Guid? AiRunId = null);
public sealed record WorkflowActionReconciliation(WorkflowActionResult? Result, bool CanRetry);
public sealed record ApprovedWorkflowProposal(WorkflowActionInvocation Invocation, Guid ReviewerId, ToolApprovalPreview Preview, Guid? AiRunId);

/// <summary>Domain actions verify the persisted approval and immutable step identity before a workflow write.</summary>
public interface IWorkflowApprovalReader
{
    Task<ApprovedWorkflowProposal> RequireApprovedProposalAsync(Guid proposalId, WorkflowActionDescriptor action,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Persisted workflow proposal verification is unavailable.");
    Task RequireActiveInvocationAsync(WorkflowActionInvocation invocation, string actionKey, int actionVersion,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Active workflow invocation verification is unavailable.");
    Task<Guid> RequireApprovedActorAsync(WorkflowActionInvocation invocation, string actionKey, int actionVersion,
        CancellationToken cancellationToken = default);
}
