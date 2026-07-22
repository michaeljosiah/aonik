using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Spec 071 §4 — the extras rail: the configured collection's members with retail
/// prices, content and option groups in one read. Empty when unconfigured, never a guess.</summary>
public class GetExtrasEndpoint : EndpointWithoutRequest<ExtrasListDto>
{
    private readonly IExtrasCatalogService _extras;

    public GetExtrasEndpoint(IExtrasCatalogService extras) => _extras = extras;

    public override void Configure()
    {
        Get("/commerce/catalog/extras");
        AllowAnonymous();
        Summary(s => s.Summary = "The add-on extras available alongside a box, with retail prices.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);
        await Send.OkAsync(await _extras.GetExtrasAsync(ct), ct);
    }
}
