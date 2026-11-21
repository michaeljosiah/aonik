using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class GetWorkflowCapabilitiesEndpoint(IWorkflowService service) : EndpointWithoutRequest<WorkflowCapabilities>
{
    public override void Configure()
    {
        Get("/ai/workflows/capabilities");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await service.GetCapabilitiesAsync(ct), ct);
}
