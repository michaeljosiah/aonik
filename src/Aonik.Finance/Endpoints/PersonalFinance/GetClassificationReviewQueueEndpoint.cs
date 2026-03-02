using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(ClassificationReviewQueueRequest req, CancellationToken ct)
    {
        var response = await _classificationService.GetReviewQueueAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
