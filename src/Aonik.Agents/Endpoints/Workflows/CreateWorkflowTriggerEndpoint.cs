using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class CreateWorkflowTriggerEndpoint(IWorkflowTriggerService triggers) : Endpoint<SaveWorkflowTriggerRequest>
{
    public override void Configure() => Post("/ai/workflows/{Slug}/triggers");
    public override async Task HandleAsync(SaveWorkflowTriggerRequest request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.SaveAsync(Route<string>("Slug")!, null, request, ct)));
}
