using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class RequestWorkflowTriggerDisableEndpoint(IWorkflowTriggerService triggers) : Endpoint<WorkflowTriggerCommand>
{
    public override void Configure() => Post("/ai/workflows/{Slug}/triggers/{BindingId:guid}/disable-requests");
    public override async Task HandleAsync(WorkflowTriggerCommand request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.RequestAsync(Route<string>("Slug")!, Route<Guid>("BindingId"), request, true, ct), accepted: true));
}
