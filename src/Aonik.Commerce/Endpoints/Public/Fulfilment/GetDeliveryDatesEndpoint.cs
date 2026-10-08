using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Endpoints.Public.Catalog;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Fulfilment;

public sealed class GetDeliveryDatesEndpoint(IFulfilmentPromiseService promises) : EndpointWithoutRequest<DeliveryDatesDto>
{
    public override void Configure()
    {
        Get("/commerce/config/delivery/dates");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Calendar-eligible delivery dates in a bounded range.";
            s.Description = "Optional fromDate defaults to the earliest date. days defaults to 31 (1–62). This read does not reserve capacity.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);
        HttpContext.Response.Headers.CacheControl = "no-store";
        var dates = await promises.GetDeliveryDatesAsync(
            Query<DateOnly?>("fromDate", isRequired: HttpContext.Request.Query.ContainsKey("fromDate")),
            Query<int?>("days", isRequired: HttpContext.Request.Query.ContainsKey("days")) ?? 31,
            ct);
        if (dates is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(dates, ct);
    }
}
