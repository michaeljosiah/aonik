using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
