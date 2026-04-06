using Aonik.Finance.Contracts.Models.Insights;
using Aonik.Finance.Contracts.Services.Insights;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Insights;

public class GetMySpaceSummaryEndpoint : EndpointWithoutRequest<MySpaceSummaryResponse>
{
    private readonly IMySpaceSummaryService _service;

    public GetMySpaceSummaryEndpoint(IMySpaceSummaryService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/insights/myspace-summary");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get MySpace dashboard summary";
            s.Description = "Returns an aggregated summary of the user's personal finance dashboard, including key metrics and highlights.";
            s.Response(200, "Summary retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.GetSummaryAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
