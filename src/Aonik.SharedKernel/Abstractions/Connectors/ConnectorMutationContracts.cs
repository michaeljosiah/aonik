using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.SharedKernel.Abstractions.Connectors;

public sealed record ConnectorMutationPreview(ToolApprovalPreview Preview, string InvocationKey);
public sealed record ConnectorMutationClaim(Guid ApprovalRequestId, Guid OperationId, Guid LeaseId, string Status,
    ConnectorUseContext Context, JsonElement? Result = null);
public sealed record ConnectorMutationStatus(Guid ApprovalRequestId, Guid? ProposalId, Guid ConnectionId, ConnectorActionRef Action,
    string Status, JsonElement? Result, string? ErrorCode);
public sealed record ConnectorMutationExecutionRequest(Guid ConnectionId, string ProviderAccountId, ConnectorActionRef Action, JsonElement Arguments,
    Guid? RunId = null, Guid? StepRunId = null, Guid? ProposalId = null, ConnectorUseContext? RecordedContext = null, Guid? OperationId = null,
    ConnectorArtifactPin? Artifact = null, int? ServiceCredentialVersion = null);
public sealed record ConnectorApprovedProposal(Guid ConnectionId, ConnectorActionRef Action, JsonElement Arguments, ConnectorUseContext Context, ToolApprovalPreview? Preview = null);

public interface IConnectorMutationService
{
    Task<JsonElement> PrepareMessageAsync(Guid connectionId, GmailPrepareMessageRequest request, CancellationToken cancellationToken = default);
    Task<ConnectorMutationPreview> PreviewAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default);
    Task<ToolApprovalPreview> PreviewWorkflowAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default);
    Task<JsonElement> ExecuteAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default);
    Task<JsonElement> ExecuteWorkflowAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default);
    Task<JsonElement> ExecuteProposalAsync(Guid proposalId, CancellationToken cancellationToken = default);
    Task<ConnectorMutationStatus?> GetStatusAsync(Guid approvalRequestId, bool reconcile = false, CancellationToken cancellationToken = default);
    Task<ToolApprovalPreview> RequireReviewAsync(Guid approvalRequestId, CancellationToken cancellationToken = default);
    Task<JsonElement?> ReconcileWorkflowAsync(Guid approvalRequestId, CancellationToken cancellationToken = default);
}

/// <summary>Agents independently verifies durable approval/execution identity; Platform keeps credentials and provider I/O.</summary>
public interface IConnectorMutationAuthority
{
    Task<ConnectorMutationPreview?> FindInvocationAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default);
    Task<ConnectorApprovedProposal> RequireProposalAsync(Guid proposalId, CancellationToken cancellationToken = default);
    Task<ConnectorApprovedProposal> RequireReviewAsync(Guid approvalRequestId, CancellationToken cancellationToken = default);
    Task<ConnectorMutationClaim> ClaimAsync(ConnectorMutationExecutionRequest request, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid approvalRequestId, Guid leaseId, JsonElement result, CancellationToken cancellationToken = default);
    Task HoldAsync(Guid approvalRequestId, Guid leaseId, string code, CancellationToken cancellationToken = default);
    Task<ConnectorMutationStatus?> GetStatusAsync(Guid approvalRequestId, CancellationToken cancellationToken = default);
    Task<ConnectorMutationExecutionRequest> RequireReconciliationAsync(Guid approvalRequestId, CancellationToken cancellationToken = default);
    Task RecordReconciliationAsync(Guid approvalRequestId, JsonElement result, CancellationToken cancellationToken = default);
}

public static class ConnectorInvocationIdentity
{
    public const string CuratedToolPrefix = "aonik_curated_";
    public const string CuratedProposalPrefix = "Connector.Curated.";
    public static bool IsCuratedTool(string toolName) => toolName.StartsWith(CuratedToolPrefix, StringComparison.Ordinal);
    public static bool IsConnectorProposal(string proposalType) => proposalType.StartsWith(CuratedProposalPrefix, StringComparison.Ordinal)
        || GmailActionPolicies.ActionForProposal(proposalType) is not null;

    public static string IdempotencyKey(JsonElement arguments)
    {
        string? key = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("idempotencyKey", out JsonElement value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (key is not { Length: > 0 and <= 128 } || !Regex.IsMatch(key, "^[A-Za-z0-9_.-]+$", RegexOptions.NonBacktracking))
            throw new ArgumentException("A stable operation key of 1–128 letters, digits, dots, underscores or hyphens is required.");
        return key;
    }

    public static string Key(Guid tenantId, Guid userId, Guid connectionId, ConnectorActionRef action, string idempotencyKey)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:D}|{userId:D}|{connectionId:D}|{action.Key}@{action.Version}|{idempotencyKey}")));
}
