using Aonik.Api.Contracts.Billing;
using Aonik.Application.Services.Billing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Billing;

public class CreateInvoiceEndpoint : Endpoint<CreateInvoiceRequest, InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public CreateInvoiceEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Post("/billing/invoices");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateInvoiceRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Billing.CreateInvoiceRequest(
            req.CustomerId,
            req.InvoiceNumber,
            req.Currency,
            req.DueUtc,
            req.LineItems.Select(li => new Application.Models.Billing.CreateInvoiceLineItemRequest(
                li.Description,
                li.Quantity,
                li.UnitPrice)).ToList());

        var result = await _billingService.CreateInvoiceAsync(appRequest, ct);

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

        await Send.CreatedAtAsync<GetInvoiceEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
