using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>The one document of storefront tunables the frontend must never hard-code
/// (Spec 070 §9). Never 404s: an unconfigured storefront gets a valid minimal document.</summary>
public class GetStorefrontConfigEndpoint : EndpointWithoutRequest<StorefrontConfigDto>
{
    private readonly IStorefrontConfigService _config;

    public GetStorefrontConfigEndpoint(IStorefrontConfigService config) => _config = config;

    public override void Configure()
    {
        Get("/commerce/config/storefront");
        AllowAnonymous();
        Summary(s => s.Summary = "Get the storefront configuration document.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);
        await Send.OkAsync(await _config.GetAsync(ct), ct);
    }
}
