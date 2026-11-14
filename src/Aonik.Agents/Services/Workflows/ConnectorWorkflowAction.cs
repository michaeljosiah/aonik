using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Uses the same gateway as Aonik's typed connector tools.</summary>
internal sealed class ConnectorWorkflowAction(ConnectorActionDescriptor action, IConnectorService connectors,
    IEnumerable<IConnectorActionProvider> providers, WorkflowInvocationContext context, IConnectorMutationService? mutations = null) : IWorkflowActionHandler
{
    public WorkflowActionDescriptor Descriptor { get; } = new(action.Key, action.Version, action.ToolName, "Integration.Use",
        action.Approval, action.InputSchema, action.OutputSchema, SupportsProposalReconciliation: GmailActionPolicies.IsMutation(action.Key));
    public void ValidateArguments(JsonElement arguments) => providers.Single(provider => provider.ConnectorKey == action.ConnectorKey)
        .ValidateArguments(new(action.Key, action.Version), arguments);
    public Task<SharedKernel.Abstractions.Agents.ToolApprovalPreview?> GetPreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => GmailActionPolicies.IsMutation(action.Key) ? PreviewAsync(invocation, cancellationToken)
            : Task.FromResult<SharedKernel.Abstractions.Agents.ToolApprovalPreview?>(null);
    private async Task<SharedKernel.Abstractions.Agents.ToolApprovalPreview?> PreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken)
        => await RequireMutations().PreviewWorkflowAsync(invocation.RunId, invocation.StepRunId, ConnectionId(), new(action.Key, action.Version), invocation.Arguments, cancellationToken);
    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        var run = context.Run ?? throw new InvalidOperationException("A leased invocation is required.");
        Guid connectionId = ConnectionId();
        try
        {
            JsonElement output = action.Approval is null ? await connectors.InvokeWorkflowReadAsync(run.Id, invocation.StepRunId, connectionId,
                new(action.Key, action.Version), invocation.Arguments, cancellationToken)
                : await RequireMutations().ExecuteWorkflowAsync(run.Id, invocation.StepRunId, connectionId, new(action.Key, action.Version), invocation.Arguments, cancellationToken);
            return new("Completed", output);
        }
        catch (PermissionDeniedException denied) when (denied.PermissionKey.StartsWith("Integration.", StringComparison.Ordinal))
        { throw new ConnectorAccessException("NoAuthorizedConnection", "The recorded actor no longer has connector access."); }
    }
    public async Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        if (!GmailActionPolicies.IsMutation(action.Key)) return new(null, true);
        if (invocation.ApprovalRequestId is not { } requestId) return new(null, false);
        JsonElement? receipt = await RequireMutations().ReconcileWorkflowAsync(requestId, cancellationToken);
        return new(receipt is { } result ? new("Completed", result) : null, false);
    }
    private IConnectorMutationService RequireMutations() => mutations ?? throw new InvalidOperationException("The enforcing connector mutation service is unavailable.");
    private Guid ConnectionId()
    {
        var definition = context.Definition ?? throw new InvalidOperationException("A published definition is required.");
        var node = definition.Nodes.Single(node => node.Key == context.Step?.NodeKey);
        var parameters = node.Parameters.Deserialize<Contracts.Models.Workflows.WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
        return parameters.Connection?.ConnectionId ?? throw new ConnectorAccessException("WorkflowBindingRequired", "Bind a connection before publishing.");
    }
}
