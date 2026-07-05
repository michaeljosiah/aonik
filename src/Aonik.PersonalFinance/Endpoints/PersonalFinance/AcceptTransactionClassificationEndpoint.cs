using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class AcceptTransactionClassificationEndpoint : EndpointWithoutRequest<ClassificationReviewItemResponse>
{
    private readonly ITransactionClassificationService _classificationService;

    public AcceptTransactionClassificationEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Post("/personal-finance/classification/review/{transactionId}/accept");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Accept a transaction classification";
            s.Description = "Accepts the AI-suggested category classification for a transaction in the review queue, confirming it as correct.";
            s.Response(200, "Classification accepted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found in review queue");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var transactionId = Route<Guid>("transactionId");

        try
        {
            var response = await _classificationService.AcceptClassificationAsync(transactionId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
