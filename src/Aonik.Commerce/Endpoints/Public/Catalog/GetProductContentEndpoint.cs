using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>
/// Spec 067 §8 — resolved content for a selection. Safety content is cacheable only with an
/// eviction story: origin computes the correct current response either way, but only stamps
/// <c>public, max-age=300</c> when the caller's <c>v</c> equals the current ContentVersion —
/// an absent or MISMATCHED v (future values included) gets <c>no-store</c>, so an anonymous
/// caller can never pre-poison a shared cache under a URL a later correction will occupy (A17).
/// </summary>
public class GetProductContentEndpoint : EndpointWithoutRequest<ResolvedContentDto>
{
    private readonly IProductService _products;
    private readonly IProductContentService _content;

    public GetProductContentEndpoint(IProductService products, IProductContentService content)
    {
        _products = products;
        _content = content;
    }

    public override void Configure()
    {
        Get("/commerce/catalog/products/{slug}/content");
        AllowAnonymous();
        Summary(s => s.Summary = "Resolve option-dependent content for a selection.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);

        // no-store FIRST, before every early exit: shared caches may cache 404s heuristically,
        // and a pre-authoring request for ?v=1 must not park a 404 under the exact URL the first
        // upsert will occupy. Only a matching successful response overwrites this below.
        HttpContext.Response.Headers.CacheControl = "no-store";

        var slug = Route<string>("slug") ?? string.Empty;
        var product = await _products.GetProductBySlugAsync(slug, ct);
        if (product is null || product.Status != ProductStatuses.Active)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        JsonDocument? selectionDocument = null;
        var selectionRaw = Query<string?>("selection", isRequired: false);
        if (!string.IsNullOrWhiteSpace(selectionRaw))
        {
            try
            {
                selectionDocument = JsonDocument.Parse(selectionRaw);
            }
            catch (JsonException)
            {
                throw new StorefrontValidationException("selection must be URL-encoded JSON.");
            }
        }

        using (selectionDocument)
        {
            var resolved = await _content.ResolveAsync(product.Id, selectionDocument?.RootElement, ct);
            if (resolved is null)
            {
                // No default block: a defined state — content is optional per product (§5 step 1).
                await Send.NotFoundAsync(ct);
                return;
            }

            var v = Query<int?>("v", isRequired: false);
            if (v == resolved.ContentVersion && StorefrontCacheHeaders.AllowsSharedCaching(HttpContext))
            {
                // Public only when the tenant discriminator is cache-visible (same class as the
                // Spec 069 promise endpoint): Vary on an absent header cannot partition.
                HttpContext.Response.Headers.CacheControl = "public, max-age=300";
            }

            await Send.OkAsync(resolved, ct);
        }
    }
}
