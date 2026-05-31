using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class UpdateInvoiceLineQuantityEndpoint : Endpoint<UpdateLineQuantityRequest, InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public UpdateInvoiceLineQuantityEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Put("/billing/invoices/{id}/lines/{lineId}/quantity");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Update a line item quantity";
            s.Description = "Updates the quantity of an existing invoice line item and recalculates totals.";
            s.Response(200, "Line item quantity updated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice line not found");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(UpdateLineQuantityRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var lineId = Route<Guid>("lineId");

        try
        {
            await _billingService.UpdateLineQuantityAsync(lineId, req.Quantity, ct);
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
