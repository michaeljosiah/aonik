using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityRetrievalEndpoint
    : Endpoint<ObservabilityQueryRequest, RetrievalResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityRetrievalEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/retrieval");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get retrieval (Qdrant + embedding) metrics";
            s.Description = "Returns per-instrument latency distributions, per-collection search stats, embedding error counts, and time series sourced from the Aonik.VectorStore meter and activity source.";
            s.Response(200, "Retrieval metrics");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetRetrievalAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
