using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class ProposeRecurringMerchantGraphAnnotationsEndpoint : Endpoint<ProposeRecurringMerchantGraphAnnotationsRequest, IReadOnlyList<FinancialLifeGraphInferenceProposalResponse>>
{
    private readonly FinancialLifeGraphInferenceService _inferenceService;

    public ProposeRecurringMerchantGraphAnnotationsEndpoint(FinancialLifeGraphInferenceService inferenceService)
    {
        _inferenceService = inferenceService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/proposals/recurring-merchants");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Propose recurring merchant annotations";
            s.Description = "Uses AI inference to propose graph annotations for recurring merchant relationships based on transaction patterns.";
            s.Response(200, "Proposals generated successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ProposeRecurringMerchantGraphAnnotationsRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _inferenceService.ProposeRecurringMerchantAnnotationsAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}
