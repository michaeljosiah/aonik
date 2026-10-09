using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

public sealed class GetDeliveryDateCapacitiesEndpoint(IDeliveryReservationService reservations)
    : EndpointWithoutRequest<IReadOnlyList<DeliveryDateCapacityDto>>
{
    public override void Configure()
    {
        Get("/commerce/admin/delivery-capacity");
        Policies("AdminReadPolicy");
        Summary(s => s.Summary = "Read explicitly configured capacities for a bounded date range.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await reservations.GetCapacitiesAsync(Query<DateOnly>("fromDate"),
            Query<int?>("days", isRequired: HttpContext.Request.Query.ContainsKey("days")) ?? 31, ct), ct);
}

public sealed class UpdateDeliveryDateCapacityEndpoint(IDeliveryReservationService reservations)
    : Endpoint<UpdateDeliveryDateCapacityRequest, DeliveryDateCapacityDto>
{
    public override void Configure()
    {
        Put("/commerce/admin/delivery-capacity/{date}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Author per-date box capacity; updates require the observed capacity version.");
    }

    public override async Task HandleAsync(UpdateDeliveryDateCapacityRequest req, CancellationToken ct)
        => await Send.OkAsync(await reservations.UpdateCapacityAsync(Route<DateOnly>("date"), req, ct), ct);
}
