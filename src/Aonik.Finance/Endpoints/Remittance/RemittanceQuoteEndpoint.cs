using Aonik.Finance.Contracts.Api.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Remittance;

/// <summary>
/// <c>POST /payabo/remittance/quote</c> — prices a corridor and persists a remittance pricing quote.
/// Authenticated customer endpoint; persists a quote only (no order/payout/ledger). Spec 036 §10.1.
/// </summary>
public class RemittanceQuoteEndpoint : Endpoint<RemittanceQuoteRequest, RemittanceQuoteResponse>
{
    private readonly IRemittanceOrderService _remittanceService;

    public RemittanceQuoteEndpoint(IRemittanceOrderService remittanceService)
    {
        _remittanceService = remittanceService;
    }

    public override void Configure()
    {
        Post("/payabo/remittance/quote");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Quote a remittance";
            s.Description = "Prices an origin→destination corridor and persists a remittance pricing quote with fee breakdown, FX metadata, expiry, and supported destination methods.";
            s.Response(200, "Quote generated successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Remittance"));
    }

    public override async Task HandleAsync(RemittanceQuoteRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _remittanceService.QuoteAsync(RemittanceMapping.ToModel(req), ct);
            await Send.OkAsync(RemittanceMapping.ToApi(result), ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
