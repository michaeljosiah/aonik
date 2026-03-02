using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
