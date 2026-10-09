using Aonik.SharedKernel.Abstractions.Payments;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

public sealed class ReconcileOrderRefundEndpoint(IOrderRefundService refunds) : EndpointWithoutRequest<RefundDto>
{
    public override void Configure()
    {
        Post("/commerce/admin/orders/{orderId:guid}/refunds/{refundId:guid}/reconcile");
        Policies("AdminWritePolicy");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        OrderRefundHeaders.Apply(HttpContext);
        var result = await refunds.ReconcileAsync(Route<Guid>("orderId"), Route<Guid>("refundId"), ct);
        await Send.ResultAsync(Results.Json(result, statusCode: 202));
    }
}
