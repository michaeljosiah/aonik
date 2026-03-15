using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetHouseholdFinanceContextAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
