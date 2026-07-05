using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "Override a transaction classification";
            s.Description = "Overrides the AI-suggested category for a transaction with a user-specified category, optionally creating a new categorisation rule.";
            s.Response(200, "Classification overridden successfully");
            s.Response(401, "Not authenticated");
            s.Response(409, "Transaction not in reviewable state");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
