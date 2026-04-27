using Aonik.Finance.Contracts.Models.Insights;
using Aonik.Finance.Contracts.Services.Insights;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Insights;

public class GetMySpaceSummaryEndpoint : Endpoint<GetMySpaceSummaryRequest, MySpaceSummaryResponse>
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
            s.Description = "Returns an aggregated summary of the user's personal finance dashboard, including key metrics and highlights. Optional `currency` query parameter overrides the cash timeline currency (must be in the tenant's configured set; otherwise the tenant primary is used).";
            s.Response(200, "Summary retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetMySpaceSummaryRequest req, CancellationToken ct)
    {
        var result = await _service.GetSummaryAsync(req.Currency, ct);
        await Send.OkAsync(result, ct);
    }
}

public sealed record GetMySpaceSummaryRequest
{
    /// <summary>
    /// Currency code (ISO 4217) to render the cash timeline in. Falls back
    /// to the tenant's primary settlement currency when omitted or when the
    /// requested code is not in the tenant's configured currency set.
    /// </summary>
    [QueryParam]
    public string? Currency { get; init; }
}
