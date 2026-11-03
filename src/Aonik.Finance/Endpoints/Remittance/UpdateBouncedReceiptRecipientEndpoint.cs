using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.SharedKernel.Abstractions.Messaging;

using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class UpdateBouncedReceiptRecipientEndpoint(IRemittanceReceiptService receipts) : Endpoint<UpdateReceiptRecipientRequest, ReceiptEmailContact>
{
    public override void Configure()
    {
        Put("/remittance/{id:guid}/receipt/email/{requestId:guid}/recipient"); Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Read"); this.RequiresPermission("Customers.Read"); this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(UpdateReceiptRecipientRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await receipts.UpdateBouncedRecipientAsync(Route<Guid>("id"), Route<Guid>("requestId"), req, ct), ct);
    }
}
