using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>One active collection by slug, members ranked (Spec 070 §10).</summary>
public class GetPublicCollectionEndpoint : EndpointWithoutRequest<PublicCollectionDto>
{
    private readonly ICollectionService _collections;

    public GetPublicCollectionEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Get("/commerce/catalog/collections/{slug}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get one curated collection by slug.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);

        var result = await _collections.GetPublicBySlugAsync(Route<string>("slug") ?? string.Empty, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
