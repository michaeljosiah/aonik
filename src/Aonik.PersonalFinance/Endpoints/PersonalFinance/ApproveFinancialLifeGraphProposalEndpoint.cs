using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Approve a graph proposal";
            s.Description = "Approves a pending AI-generated graph proposal, applying the proposed nodes and edges to the financial life graph.";
            s.Response(204, "Proposal approved and applied successfully");
            s.Response(401, "Not authenticated");
            s.Response(409, "Proposal is not in a pending state");
        });
        Options(x => x.WithTags("Personal Finance"));
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
