using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed partial class ConnectorMutationService
{
    public async Task<ConnectorMutationStatus?> GetStatusAsync(Guid approvalRequestId, bool reconcile = false, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        ConnectorMutationStatus? status = await authority.GetStatusAsync(approvalRequestId, cancellationToken);
        if (status is null) return null;
        await authorization.RequireUseAsync(status.ConnectionId, catalog.Resolve(status.Action), new(new("User", userId, userId)), cancellationToken);
        if (reconcile && status.Status == "NeedsAttention")
        {
            await ReconcileCoreAsync(approvalRequestId, cancellationToken);
            status = await authority.GetStatusAsync(approvalRequestId, cancellationToken);
        }
        return status;
    }

    public Task<JsonElement?> ReconcileWorkflowAsync(Guid approvalRequestId, CancellationToken cancellationToken = default)
        => ReconcileCoreAsync(approvalRequestId, cancellationToken);

    private async Task<JsonElement?> ReconcileCoreAsync(Guid approvalRequestId, CancellationToken cancellationToken)
    {
        ConnectorMutationExecutionRequest request = await authority.RequireReconciliationAsync(approvalRequestId, cancellationToken);
        ConnectorUseContext context = request.RecordedContext ?? throw ConnectorAuthorization.Access("WorkflowBindingRequired");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(60));
        ConnectorProviderCall call = await CallAsync(request.ConnectionId, request.Action, request.Arguments, context,
            request.OperationId ?? throw ConnectorAuthorization.Access("ActionOutcomeUnknown"), budget.Token);
        if (call.ProviderAccountId != request.ProviderAccountId) throw ConnectorAuthorization.Access("ReviewedAccountChanged");
        ConnectorMutationStatus status = await authority.GetStatusAsync(approvalRequestId, budget.Token) ?? throw ConnectorAuthorization.Access("ActionOutcomeUnknown");
        if (status.Status == "Completed") return status.Result;
        ConnectorMutationReconciliation result = await Provider(request.Action).ReconcileAsync(call, budget.Token);
        await authorization.RequireUseAsync(request.ConnectionId, catalog.Resolve(request.Action), context, budget.Token);
        if (result.Result is { } verified) await authority.RecordReconciliationAsync(approvalRequestId, verified, budget.Token);
        await RecordAsync(result.Result.HasValue ? "Connector.MutationReconciled" : "Connector.MutationUnresolved", request.ConnectionId,
            request.Action, call.OperationId, context, null, budget.Token);
        return result.Result;
    }
}
