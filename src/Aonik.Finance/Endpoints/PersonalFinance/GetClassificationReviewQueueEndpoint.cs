using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetClassificationReviewQueueEndpoint : Endpoint<ClassificationReviewQueueRequest, IReadOnlyList<ClassificationReviewItemResponse>>
{
    private readonly ITransactionClassificationService _classificationService;

    public GetClassificationReviewQueueEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Get("/personal-finance/classification/review-queue");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get classification review queue";
            s.Description = "Returns transactions with AI-suggested category classifications that are pending user review and confirmation.";
            s.Response(200, "Review queue returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ClassificationReviewQueueRequest req, CancellationToken ct)
    {
        var response = await _classificationService.GetReviewQueueAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
