using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Reject a graph proposal";
            s.Description = "Rejects a pending AI-generated graph proposal with an optional reason, preventing it from being applied.";
            s.Response(204, "Proposal rejected successfully");
            s.Response(401, "Not authenticated");
            s.Response(409, "Proposal is not in a pending state");
        });
        Options(x => x.WithTags("Personal Finance"));
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
