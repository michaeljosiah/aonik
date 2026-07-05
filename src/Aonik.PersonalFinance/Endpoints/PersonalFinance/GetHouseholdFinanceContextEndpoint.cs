using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class GetHouseholdFinanceContextEndpoint : EndpointWithoutRequest<HouseholdFinanceContextResponse>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetHouseholdFinanceContextEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/household-context");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get household finance context";
            s.Description = "Returns the financial context for the user's household, including shared accounts, combined spending, and member contributions.";
            s.Response(200, "Household finance context returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetHouseholdFinanceContextAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
