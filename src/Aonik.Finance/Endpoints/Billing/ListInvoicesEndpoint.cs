using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Billing;

public class ListInvoicesRequest
{
    [QueryParam]
    public string? Status { get; set; }
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
    }

    public override async Task HandleAsync(ListInvoicesRequest req, CancellationToken ct)
    {
        var result = await _billingService.ListInvoicesAsync(req.Status, ct);
        await Send.OkAsync(result.ToList(), ct);
    }
}
