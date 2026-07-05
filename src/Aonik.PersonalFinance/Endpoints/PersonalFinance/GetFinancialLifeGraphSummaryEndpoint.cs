using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class GetFinancialLifeGraphSummaryEndpoint : EndpointWithoutRequest<FinancialLifeGraphSummaryResponse>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetFinancialLifeGraphSummaryEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/summary");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get financial life graph summary";
            s.Description = "Returns a high-level summary of the financial life graph, including node and edge counts and key statistics.";
            s.Response(200, "Graph summary returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetGraphSummaryAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
