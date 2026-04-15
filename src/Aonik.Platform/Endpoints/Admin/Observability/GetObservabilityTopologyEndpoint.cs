using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityTopologyEndpoint
    : Endpoint<ObservabilityQueryRequest, TopologyResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityTopologyEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/topology");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get service topology graph";
            s.Description = "Returns a node/edge graph assembled from the Application Insights requests + dependencies tables. Nodes are services or external targets; edges carry calls/error-rate/p95 for a quick health overview.";
            s.Response(200, "Topology graph");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetTopologyAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
