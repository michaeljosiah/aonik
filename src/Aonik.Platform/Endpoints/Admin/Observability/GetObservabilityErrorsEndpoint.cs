using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal class GetObservabilityErrorsEndpoint
    : Endpoint<ObservabilityQueryRequest, ErrorsResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityErrorsEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/errors");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get error groups";
            s.Description = "Returns top error groups from Application Insights exceptions.";
            s.Response(200, "Error groups");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ObservabilityQueryRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetErrorsAsync(req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
