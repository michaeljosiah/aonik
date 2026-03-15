using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetPendingFinancialLifeGraphProposalsEndpoint : EndpointWithoutRequest<IReadOnlyList<PendingFinancialLifeGraphProposalResponse>>
{
    private readonly FinancialLifeGraphInferenceService _inferenceService;

    public GetPendingFinancialLifeGraphProposalsEndpoint(FinancialLifeGraphInferenceService inferenceService)
    {
        _inferenceService = inferenceService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/proposals/pending");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _inferenceService.ListPendingProposalsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
