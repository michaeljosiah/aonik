using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityAiEndpoint
    : Endpoint<ObservabilityQueryRequest, AiMetricsResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityAiEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/ai");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get AI metrics";
            s.Description = "Returns AI/LLM call metrics from Application Insights dependencies.";
            s.Response(200, "AI metrics");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetAiMetricsAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
