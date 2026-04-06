using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Generate a spending narrative";
            s.Description = "Uses AI to generate a natural-language narrative summarising the user's spending patterns for a specified period.";
            s.Response(200, "Spending narrative generated successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
