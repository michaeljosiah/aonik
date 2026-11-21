using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class RequestWorkflowTriggerActivationEndpoint(IWorkflowTriggerService triggers) : Endpoint<WorkflowTriggerCommand>
{
    public override void Configure() => Post("/ai/workflows/{Slug}/triggers/{BindingId:guid}/activation-requests");
    public override async Task HandleAsync(WorkflowTriggerCommand request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.RequestAsync(Route<string>("Slug")!, Route<Guid>("BindingId"), request, false, ct), accepted: true));
}
