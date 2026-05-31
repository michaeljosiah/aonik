using Aonik.Authorization;
using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

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
        Policies("AdminUserWritePolicy");
        // Declarative permission gate — cannot be silently bypassed by
        // forgetting to call EnsurePermissionAsync inside the service.
        this.RequiresPermission("Invoice.Create");
        Summary(s =>
        {
            s.Summary = "Create a new invoice";
            s.Description = "Creates a new invoice for a customer with the specified line items and currency.";
            s.Response(201, "Invoice created successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(403, "Missing required permission");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(CreateInvoiceRequest req, CancellationToken ct)
    {
        var appRequest = new Finance.Contracts.Models.Billing.CreateInvoiceRequest(
            req.CustomerId,
            req.InvoiceNumber,
            req.Currency,
            req.DueUtc,
            req.LineItems.Select(li => new Finance.Contracts.Models.Billing.CreateInvoiceLineItemRequest(
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
