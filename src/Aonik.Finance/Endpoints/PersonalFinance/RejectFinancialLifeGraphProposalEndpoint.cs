using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class RejectFinancialLifeGraphProposalEndpoint : Endpoint<RejectFinancialLifeGraphProposalRequest>
{
    private readonly FinancialLifeGraphInferenceService _inferenceService;

    public RejectFinancialLifeGraphProposalEndpoint(FinancialLifeGraphInferenceService inferenceService)
    {
        _inferenceService = inferenceService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/proposals/{proposalId:guid}/reject");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(RejectFinancialLifeGraphProposalRequest req, CancellationToken ct)
    {
        try
        {
            await _inferenceService.RejectProposalAsync(Route<Guid>("proposalId"), req.Reason, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
