using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class GetObservabilityErrorDetailRequest
{
    public string ProblemId { get; init; } = string.Empty;

    [QueryParam]
    public string TimeRange { get; init; } = "24h";
}

/// <summary>
/// Drill-down for a single error group. Pulls one representative exception
/// from the App Insights <c>exceptions</c> table and returns its full
/// parsed stack trace, operation context, and custom dimensions so the
/// admin UI can render enough detail for a developer to act on the error
/// without opening the Azure Portal.
/// </summary>
internal class GetObservabilityErrorDetailEndpoint
    : Endpoint<GetObservabilityErrorDetailRequest, ErrorDetailResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetObservabilityErrorDetailEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/errors/{ProblemId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get error detail";
            s.Description = "Returns a representative exception for the given problemId, including parsed stack trace and custom dimensions.";
            s.Response(200, "Error detail");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(GetObservabilityErrorDetailRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetErrorDetailAsync(req.ProblemId, req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
