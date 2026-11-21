using System.Text.Json;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record SimulateWorkflowRequest(
    string Slug, IReadOnlyDictionary<string, JsonElement> Facts,
    IReadOnlyDictionary<string, JsonElement>? ActionOutputs = null);

internal sealed class SimulateWorkflowEndpoint(IWorkflowService service) : Endpoint<SimulateWorkflowRequest, WorkflowSimulation>
{
    public override void Configure()
    {
        Post("/ai/workflows/{Slug}/simulate");
    }

    public override async Task HandleAsync(SimulateWorkflowRequest req, CancellationToken ct)
    {
        try
        {
            WorkflowSimulation? result = await service.SimulateAsync(Route<string>("Slug")!, new(req.Facts, req.ActionOutputs), ct);
            if (result is null) await Send.NotFoundAsync(ct);
            else await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            await Send.ResultAsync(Results.BadRequest(new { error = ex.Message }));
        }
    }
}
