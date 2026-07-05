using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class GetMerchantHistoryRequest
{
    [QueryParam]
    public string Merchant { get; set; } = string.Empty;
}

internal sealed class GetMerchantHistoryEndpoint : Endpoint<GetMerchantHistoryRequest, MerchantHistoryResponse>
{
    private readonly IPersonalFinanceInsightsService _insightsService;

    public GetMerchantHistoryEndpoint(IPersonalFinanceInsightsService insightsService)
    {
        _insightsService = insightsService;
    }

    public override void Configure()
    {
        Get("/personal-finance/spending/merchants/history");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get merchant transaction history";
            s.Description = "Returns the full transaction history for a specific merchant, including total spend, transaction count, and frequency patterns.";
            s.Response(200, "Merchant history returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Merchant name is required");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetMerchantHistoryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Merchant))
        {
            ThrowError("Merchant name is required.", 422);
        }

        var response = await _insightsService.GetMerchantHistoryAsync(req.Merchant, ct);
        await Send.OkAsync(response, ct);
    }
}
