using Aonik.Authorization;
using Aonik.Finance.Contracts.Api.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Remittance;

/// <summary>
/// <c>POST /remittance/quote</c> — prices a corridor and persists a remittance pricing quote.
/// Permission-gated operator endpoint; persists a quote only (no order/payout/ledger). Spec 036 §10.1.
/// </summary>
public class OperatorRemittanceQuoteEndpoint : Endpoint<RemittanceQuoteRequest, RemittanceQuoteResponse>
{
    private readonly IOperatorRemittanceService _remittanceService;

    public OperatorRemittanceQuoteEndpoint(IOperatorRemittanceService remittanceService)
    {
        _remittanceService = remittanceService;
    }

    public override void Configure()
    {
        Post("/remittance/quote");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Create");
        Summary(s =>
        {
            s.Summary = "Quote a remittance";
            s.Description = "Prices an origin→destination corridor and persists a remittance pricing quote with fee breakdown, FX metadata, expiry, and supported destination methods.";
            s.Response(200, "Quote generated successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(403, "Staff policy or required payment permission is missing");
        });
        Options(x => x.WithTags("Remittance"));
    }

    public override async Task HandleAsync(RemittanceQuoteRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _remittanceService.QuoteForCustomerAsync(RemittanceMapping.ToModel(req), ct);
            await Send.OkAsync(RemittanceMapping.ToApi(result), ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(403, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
