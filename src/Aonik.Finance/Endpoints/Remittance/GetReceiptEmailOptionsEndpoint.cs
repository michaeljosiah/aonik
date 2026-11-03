using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class GetReceiptEmailOptionsEndpoint(IRemittanceReceiptService receipts) : EndpointWithoutRequest<ReceiptEmailOptions>
{
    public override void Configure()
    {
        Get("/remittance/{id:guid}/receipt/email");Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Read");this.RequiresPermission("Customers.Read");this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await receipts.GetEmailOptionsAsync(Route<Guid>("id"), ct), ct);
    }
}
