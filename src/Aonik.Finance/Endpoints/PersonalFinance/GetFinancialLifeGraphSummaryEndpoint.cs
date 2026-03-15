using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetGraphSummaryAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
