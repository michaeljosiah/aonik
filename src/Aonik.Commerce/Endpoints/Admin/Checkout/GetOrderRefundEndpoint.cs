using Aonik.SharedKernel.Abstractions.Payments;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

public sealed class GetOrderRefundEndpoint(IOrderRefundService refunds) : EndpointWithoutRequest<RefundDto>
{
    public override void Configure()
    {
        Get("/commerce/admin/orders/{orderId:guid}/refunds/{refundId:guid}");
        Policies("AdminReadPolicy");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        OrderRefundHeaders.Apply(HttpContext);
        var result = await refunds.GetRefundAsync(Route<Guid>("orderId"), Route<Guid>("refundId"), ct);
        if (result is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(result, ct);
    }
}
