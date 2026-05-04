using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class GetObservabilityLogsEndpoint
    : Endpoint<ObservabilityQueryRequest, StructuredLogsResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityLogsEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/logs");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get structured logs";
            s.Description = "Returns structured log volume and recent entries from Application Insights traces.";
            s.Response(200, "Structured logs");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetStructuredLogsAsync(req.TimeRange, req.Severity, ct);
        await Send.OkAsync(result, ct);
    }
}
