using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

public sealed class UpdateOrderFulfilmentEndpoint(IOrderFulfilmentService fulfilment)
    : Endpoint<UpdateOrderFulfilmentCommand, OrderFulfilmentDto>
{
    public override void Configure()
    {
        Put("/commerce/admin/orders/{orderId:guid}/fulfilment");
        Policies("AdminWritePolicy");
    }

    public override async Task HandleAsync(UpdateOrderFulfilmentCommand req, CancellationToken ct)
        => await Send.OkAsync(await fulfilment.UpdateAsync(Route<Guid>("orderId"), req, ct), ct);
}
