using FastEndpoints;

using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class ValidateWorkflowTriggerEndpoint(IWorkflowTriggerService triggers) : Endpoint<WorkflowTriggerReadHttpRequest>
{
    public override void Configure() => Post("/ai/workflows/{Slug}/triggers/{BindingId:guid}/validate");
    public override async Task HandleAsync(WorkflowTriggerReadHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.ValidateAsync(request.Slug, request.BindingId, ct)));
}
