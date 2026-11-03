using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;

using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class GetBouncedReceiptRecipientEndpoint(IRemittanceReceiptService receipts) : EndpointWithoutRequest<ReceiptRecipientReview>
{
    public override void Configure()
    {
        Get("/remittance/{id:guid}/receipt/email/{requestId:guid}/recipient"); Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Read"); this.RequiresPermission("Customers.Read"); this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await receipts.GetBouncedRecipientAsync(Route<Guid>("id"), Route<Guid>("requestId"), ct), ct);
    }
}
