using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Active collections with ranked Active member summaries (Spec 070 §10) — the
/// homepage rails render this verbatim.</summary>
public class ListPublicCollectionsEndpoint : EndpointWithoutRequest<IReadOnlyList<PublicCollectionDto>>
{
    private readonly ICollectionService _collections;

    public ListPublicCollectionsEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Get("/commerce/catalog/collections");
        AllowAnonymous();
        Summary(s => s.Summary = "Browse the storefront's curated collections.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);

        var result = await _collections.ListPublicAsync(Query<string?>("kind", isRequired: false), ct);
        await Send.OkAsync(result, ct);
    }
}
