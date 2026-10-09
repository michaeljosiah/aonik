using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Payments;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

public sealed class PreviewOrderRefundEndpoint(IOrderRefundService refunds) : Endpoint<RefundDraft, RefundPreviewDto>
{
    public override void Configure()
    {
        Post("/commerce/admin/orders/{orderId:guid}/refunds/preview");
        Policies("AdminWritePolicy");
    }
    public override async Task HandleAsync(RefundDraft req, CancellationToken ct)
    {
        OrderRefundHeaders.Apply(HttpContext);
        try { await Send.OkAsync(await refunds.PreviewAsync(Route<Guid>("orderId"), req, ct), ct); }
        catch (InvalidStateException exception)
        {
            AddError(exception.Message);
            await Send.ErrorsAsync(statusCode: 409, cancellation: ct);
        }
    }
}
