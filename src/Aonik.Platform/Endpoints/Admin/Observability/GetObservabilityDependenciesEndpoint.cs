using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityDependenciesEndpoint
    : Endpoint<ObservabilityQueryRequest, DependencyMetricsResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityDependenciesEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/dependencies");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get dependency health";
            s.Description = "Returns dependency health metrics from Application Insights.";
            s.Response(200, "Dependency health");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetDependenciesAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
