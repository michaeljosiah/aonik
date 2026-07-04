using Aonik.Finance.Contracts.Api.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Billing;

public class ListInvoicesRequest
{
    [QueryParam]
    public string? Status { get; set; }

    /// <summary>1-based page number (defaults to the first page).</summary>
    [QueryParam]
    public int PageNumber { get; set; } = 1;

    /// <summary>Page size; server-capped so a single call can't return an unbounded set (issue H10).</summary>
    [QueryParam]
    public int PageSize { get; set; } = 200;
}

public class ListInvoicesEndpoint : Endpoint<ListInvoicesRequest, List<InvoiceResponse>>
{
    private readonly IBillingService _billingService;

    public ListInvoicesEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }

    public override void Configure()
    {
        Get("/billing/invoices");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List invoices";
            s.Description = "Returns a list of invoices, optionally filtered by status.";
            s.Response(200, "Invoices retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Billing"));
    }

    public override async Task HandleAsync(ListInvoicesRequest req, CancellationToken ct)
    {
        var result = await _billingService.ListInvoicesAsync(req.Status, req.PageNumber, req.PageSize, ct);

        var response = result.Select(r => new InvoiceResponse(
            r.Id,
            r.CustomerId,
            r.InvoiceNumber,
            r.Currency,
            r.TotalAmount,
            r.Status.ToString(),
            r.IssuedUtc,
            r.DueUtc,
            r.LineItems.Select(li => new InvoiceLineItemResponse(
                li.Id,
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.LineTotal)).ToList())).ToList();

        await Send.OkAsync(response, ct);
    }
}
