using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
