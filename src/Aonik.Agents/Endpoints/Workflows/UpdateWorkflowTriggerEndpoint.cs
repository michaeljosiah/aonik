using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class UpdateWorkflowTriggerEndpoint(IWorkflowTriggerService triggers) : Endpoint<SaveWorkflowTriggerRequest>
{
    public override void Configure() => Put("/ai/workflows/{Slug}/triggers/{BindingId:guid}");
    public override async Task HandleAsync(SaveWorkflowTriggerRequest request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.SaveAsync(Route<string>("Slug")!, Route<Guid>("BindingId"), request, ct)));
}
