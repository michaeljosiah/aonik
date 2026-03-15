using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ApproveFinancialLifeGraphProposalEndpoint : EndpointWithoutRequest
{
    private readonly FinancialLifeGraphInferenceService _inferenceService;

    public ApproveFinancialLifeGraphProposalEndpoint(FinancialLifeGraphInferenceService inferenceService)
    {
        _inferenceService = inferenceService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/proposals/{proposalId:guid}/approve");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _inferenceService.ApproveProposalAsync(Route<Guid>("proposalId"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
