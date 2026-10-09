using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Endpoints.Public.Catalog;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Fulfilment;

/// <summary>
/// Spec 069 §6 — the earliest-delivery promise, shown everywhere it appears on the storefront.
/// Capacity changes on every reservation, so the response is never cached. Tenant-partitioned via <c>Vary: X-Tenant-Id</c> —
/// a shared cache serving tenant A's promise to tenant B would contradict A7 outright. 404 when
/// unconfigured: a wrong date is worse than no date; the endpoint never guesses.
/// </summary>
public class GetDeliveryConfigEndpoint : EndpointWithoutRequest<FulfilmentPromiseDto>
{
    private readonly IFulfilmentPromiseService _promises;

    public GetDeliveryConfigEndpoint(IFulfilmentPromiseService promises) => _promises = promises;

    public override void Configure()
    {
        Get("/commerce/config/delivery");
        AllowAnonymous();
        Summary(s => s.Summary = "The earliest eligible delivery date with capacity for a box.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);
        HttpContext.Response.Headers.CacheControl = "no-store";

        var promise = await _promises.GetEarliestDeliveryAsync(ct);
        if (promise is null)
        {
            // An unconfigured→configured transition must not be parked in a shared cache.
            HttpContext.Response.Headers.CacheControl = "no-store";
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(promise, ct);
    }
}
