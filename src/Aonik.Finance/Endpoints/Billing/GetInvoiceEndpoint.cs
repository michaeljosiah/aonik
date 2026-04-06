using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

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
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get an invoice by ID";
            s.Description = "Retrieves a single invoice by its unique identifier, including line items.";
            s.Response(200, "Invoice retrieved successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice not found");
        });
        Options(x => x.WithTags("Billing"));
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
