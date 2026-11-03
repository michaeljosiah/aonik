using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class GetRemittanceReceiptEndpoint(IRemittanceReceiptService receipts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/remittance/{id:guid}/receipt");Policies("AdminReadPolicy");this.RequiresPermission("Payment.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var file = await receipts.DownloadAsync(Route<Guid>("id"), ct);
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.BytesAsync(file.Content, fileName: file.FileName, contentType: "application/pdf", cancellation: ct);
    }
}
