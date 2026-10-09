using Aonik.SharedKernel.Abstractions.Payments;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

public sealed class GetOrderRefundsEndpoint(IOrderRefundService refunds) : EndpointWithoutRequest<RefundContextDto>
{
    public override void Configure()
    {
        Get("/commerce/admin/orders/{orderId:guid}/refunds");
        Policies("AdminReadPolicy");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        OrderRefundHeaders.Apply(HttpContext);
        await Send.OkAsync(await refunds.GetAsync(Route<Guid>("orderId"), ct), ct);
    }
}
