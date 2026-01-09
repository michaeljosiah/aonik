using Aonik.Api.Contracts.Billing;
using Aonik.Application.Services.Billing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Billing;

public class GetInvoiceEndpoint : EndpointWithoutRequest<InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public GetInvoiceEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Get("/billing/invoices/{id}");
        Policies("Invoice.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _billingService.GetInvoiceAsync(id, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new InvoiceResponse(
            result.Id,
            result.CustomerId,
            result.InvoiceNumber,
            result.Currency,
            result.TotalAmount,
            result.Status.ToString(),
            result.IssuedUtc,
            result.DueUtc,
            result.LineItems.Select(li => new InvoiceLineItemResponse(
                li.Id,
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.LineTotal)).ToList());

        await Send.OkAsync(response, ct);
    }
}
