using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityJobsEndpoint
    : Endpoint<ObservabilityQueryRequest, JobMetricsResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityJobsEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/jobs");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get job execution metrics";
            s.Description = "Returns background job execution metrics from Application Insights custom events.";
            s.Response(200, "Job metrics");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetJobMetricsAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
