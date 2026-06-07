using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class IssueInvoiceEndpoint : EndpointWithoutRequest<InvoiceResponse>
{
    private readonly IBillingService _billingService;

    public IssueInvoiceEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Post("/billing/invoices/{id}/issue");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Issue a draft invoice";
            s.Description = "Transitions a draft invoice to the issued state, making it active and payable.";
            s.Response(200, "Invoice issued successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invoice not found");
            s.Response(422, "Invoice cannot be issued (not in Draft status)");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        await _billingService.IssueInvoiceAsync(id, ct);

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
