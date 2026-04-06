using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetFinancialLifeGraphEndpoint : EndpointWithoutRequest<FinancialLifeGraphResponse>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetFinancialLifeGraphEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get the financial life graph";
            s.Description = "Returns the full financial life graph containing nodes (accounts, merchants, people) and edges (relationships, transactions) for the authenticated user.";
            s.Response(200, "Financial life graph returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetGraphAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
