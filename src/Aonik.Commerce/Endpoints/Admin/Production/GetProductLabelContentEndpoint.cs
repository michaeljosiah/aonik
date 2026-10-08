using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

/// <summary>Production labels share the storefront's exact-selection declaration rules.</summary>
public class GetProductLabelContentEndpoint : EndpointWithoutRequest<ResolvedContentDto>
{
    private readonly IProductContentService _content;

    public GetProductLabelContentEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/label-content");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Resolve the authored ingredients and allergens for a production label's exact selection.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";

        var productId = Route<Guid>("productId");
        // Reuse the tenant-scoped, status-agnostic authoring read to verify the product exists.
        await _content.GetAdminAsync(productId, ct);

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
            var resolved = await _content.ResolveAsync(productId, selectionDocument?.RootElement, ct);
            if (resolved is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(resolved, ct);
        }
    }
}
