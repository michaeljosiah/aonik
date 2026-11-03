using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class GetReceiptEmailStatusEndpoint(IRemittanceReceiptService receipts) : EndpointWithoutRequest<ReceiptEmailResult>
{
    public override void Configure()
    {
        Get("/remittance/{id:guid}/receipt/email/{requestId:guid}");Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Read");this.RequiresPermission("Customers.Read");this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var result = await receipts.GetSendStatusAsync(Route<Guid>("id"), Route<Guid>("requestId"), ct);
        if (result is null) { await Send.NotFoundAsync(ct);return; }
        await Send.OkAsync(result, ct);
    }
}
