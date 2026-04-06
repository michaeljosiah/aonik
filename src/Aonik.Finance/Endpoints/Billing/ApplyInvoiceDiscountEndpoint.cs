using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class ApplyInvoiceDiscountEndpoint : Endpoint<ApplyDiscountRequest, InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public ApplyInvoiceDiscountEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Post("/billing/invoices/{id}/discount");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Apply a discount to an invoice";
            s.Description = "Sets the discount amount on an invoice and recalculates totals.";
            s.Response(200, "Discount applied successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice not found");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(ApplyDiscountRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _billingService.ApplyDiscountAsync(id, req.DiscountTotal, ct);
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
