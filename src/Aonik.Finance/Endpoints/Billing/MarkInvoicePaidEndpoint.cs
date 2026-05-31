using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class MarkInvoicePaidEndpoint : EndpointWithoutRequest<InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public MarkInvoicePaidEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Post("/billing/invoices/{id}/mark-paid");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Mark an invoice as paid";
            s.Description = "Transitions an issued invoice to the paid state.";
            s.Response(200, "Invoice marked as paid");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice not found");
            s.Response(422, "Invoice cannot be marked as paid (not in Issued status)");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _billingService.MarkInvoiceAsPaidAsync(id, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(ex.Message.Contains("not found") ? 404 : 422, ct);
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
