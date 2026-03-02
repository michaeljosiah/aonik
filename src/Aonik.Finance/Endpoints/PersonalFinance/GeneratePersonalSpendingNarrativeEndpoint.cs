using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GeneratePersonalSpendingNarrativeEndpoint : Endpoint<GeneratePersonalSpendingNarrativeRequest, PersonalSpendingNarrativeInsightResponse>
{
    private readonly IPersonalFinanceNarrativeInsightsService _narrativeInsightsService;

    public GeneratePersonalSpendingNarrativeEndpoint(IPersonalFinanceNarrativeInsightsService narrativeInsightsService)
    {
        _narrativeInsightsService = narrativeInsightsService;
    }

    public override void Configure()
    {
        Post("/personal-finance/insights/narrative");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(GeneratePersonalSpendingNarrativeRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _narrativeInsightsService.GenerateSpendingNarrativeAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}
