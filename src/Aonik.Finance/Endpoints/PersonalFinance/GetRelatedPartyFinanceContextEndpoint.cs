using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetRelatedPartyFinanceContextAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
