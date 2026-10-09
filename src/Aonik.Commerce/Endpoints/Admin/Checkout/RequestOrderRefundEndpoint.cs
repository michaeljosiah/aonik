using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Payments;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

public sealed class RequestOrderRefundEndpoint(IOrderRefundService refunds) : Endpoint<RefundRequest, RefundDto>
{
    public override void Configure()
    {
        Post("/commerce/admin/orders/{orderId:guid}/refunds");
        Policies("AdminWritePolicy");
    }
    public override async Task HandleAsync(RefundRequest req, CancellationToken ct)
    {
        OrderRefundHeaders.Apply(HttpContext);
        try
        {
            var result = await refunds.RequestAsync(Route<Guid>("orderId"), req, ct);
            await Send.ResultAsync(Results.Json(result, statusCode: 202));
        }
        catch (InvalidStateException exception)
        {
            AddError(exception.Message);
            await Send.ErrorsAsync(statusCode: 409, cancellation: ct);
        }
    }
}
