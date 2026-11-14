using FastEndpoints;

using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class GetWorkflowTriggerEndpoint(IWorkflowTriggerService triggers) : Endpoint<WorkflowTriggerReadHttpRequest>
{
    public override void Configure() => Get("/ai/workflows/{Slug}/triggers/{BindingId:guid}");
    public override async Task HandleAsync(WorkflowTriggerReadHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.GetAsync(request.Slug, request.BindingId, ct)));
}
