using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetRelatedPartyFinanceContextEndpoint : EndpointWithoutRequest<RelatedPartyFinanceContextResponse>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetRelatedPartyFinanceContextEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/related-party-context");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get related party finance context";
            s.Description = "Returns the financial context for parties related to the user, such as shared merchants, recurring payees, and transfer counterparties.";
            s.Response(200, "Related party finance context returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetRelatedPartyFinanceContextAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
