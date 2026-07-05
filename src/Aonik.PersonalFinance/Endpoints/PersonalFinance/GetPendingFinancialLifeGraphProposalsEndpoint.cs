using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "List pending graph proposals";
            s.Description = "Returns all AI-generated financial life graph proposals that are awaiting user approval or rejection.";
            s.Response(200, "Pending proposals returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _inferenceService.ListPendingProposalsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
