using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class AddInvoiceLineEndpoint : Endpoint<AddInvoiceLineRequest, InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public AddInvoiceLineEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Post("/billing/invoices/{id}/lines");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Add a line item to an invoice";
            s.Description = "Adds a new line item to an existing invoice and recalculates totals.";
            s.Response(200, "Line item added successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice not found");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(AddInvoiceLineRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var lineRequest = new Contracts.Models.Billing.CreateInvoiceLineItemRequest(
                req.Description, req.Quantity, req.UnitPrice);
            await _billingService.AddLineToInvoiceAsync(id, lineRequest, ct);
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
