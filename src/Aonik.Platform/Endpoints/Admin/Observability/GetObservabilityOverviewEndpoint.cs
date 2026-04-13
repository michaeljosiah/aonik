using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityOverviewEndpoint
    : Endpoint<ObservabilityQueryRequest, ObservabilityOverviewResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityOverviewEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/overview");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get observability overview";
            s.Description = "Returns request, error, and latency metrics from Application Insights.";
            s.Response(200, "Overview metrics");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetOverviewAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
