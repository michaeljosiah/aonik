using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class UpdateInvoiceLineUnitPriceEndpoint : Endpoint<UpdateLineUnitPriceRequest, InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public UpdateInvoiceLineUnitPriceEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Put("/billing/invoices/{id}/lines/{lineId}/unit-price");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a line item unit price";
            s.Description = "Updates the unit price of an existing invoice line item and recalculates totals.";
            s.Response(200, "Line item price updated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice line not found");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(UpdateLineUnitPriceRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var lineId = Route<Guid>("lineId");

        try
        {
            await _billingService.UpdateLineUnitPriceAsync(lineId, req.UnitPrice, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var result = await _billingService.GetInvoiceAsync(id, ct);

        var response = new InvoiceResponse(
            result!.Id,
            result.CustomerId,
            result.InvoiceNumber,
            result.Currency,
            result.TotalAmount,
            result.Status.ToString(),
            result.IssuedUtc,
            result.DueUtc,
            result.LineItems.Select(li => new InvoiceLineItemResponse(
                li.Id, li.Description, li.Quantity, li.UnitPrice, li.LineTotal)).ToList());

        await Send.OkAsync(response, ct);
    }
}
