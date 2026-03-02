using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class OverrideTransactionClassificationEndpoint : Endpoint<OverrideTransactionClassificationRequest, ClassificationReviewItemResponse>
{
    private readonly ITransactionClassificationService _classificationService;

    public OverrideTransactionClassificationEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Post("/personal-finance/classification/review/{transactionId}/override");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(OverrideTransactionClassificationRequest req, CancellationToken ct)
    {
        var transactionId = Route<Guid>("transactionId");

        try
        {
            var response = await _classificationService.OverrideClassificationAsync(transactionId, req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
