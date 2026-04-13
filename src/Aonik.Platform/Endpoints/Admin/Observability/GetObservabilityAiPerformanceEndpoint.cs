using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityAiPerformanceEndpoint
    : Endpoint<ObservabilityQueryRequest, AiPerformanceResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityAiPerformanceEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/ai-performance");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get AI performance metrics";
            s.Description = "Returns AI agent performance metrics including latency distributions, TTFT percentiles, token usage, per-agent breakdowns, and client vs server timing from Application Insights.";
            s.Response(200, "AI performance metrics");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetAiPerformanceAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
