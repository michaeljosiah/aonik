using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>
/// The public option catalogue — servable groups only, ordered, with their choices, prices and
/// recommended default (Spec 066 §11). The storefront's global personaliser reference; a product
/// page narrows it through the product's own effective options.
/// </summary>
public class GetOptionCatalogueEndpoint : EndpointWithoutRequest<IReadOnlyList<OptionGroupDto>>
{
    private readonly IProductOptionService _options;

    public GetOptionCatalogueEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Get("/commerce/catalog/options");
        AllowAnonymous();
        Summary(s => s.Summary = "Get the public option catalogue.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);

        var result = await _options.GetCatalogueAsync(includeInactive: false, ct);
        await Send.OkAsync(result, ct);
    }
}
