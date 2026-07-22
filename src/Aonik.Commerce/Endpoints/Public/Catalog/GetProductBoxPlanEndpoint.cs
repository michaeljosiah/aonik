using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Spec 068 §11 — the size plan for a bundle product: min/max, presets
/// (badge/blurb/saving), per-space price, currency. Step 1's entire pricing UI, the homepage's
/// box offers and the grow-flow's maths in one read.</summary>
public class GetProductBoxPlanEndpoint : EndpointWithoutRequest<BoxPlanDto>
{
    private readonly IBundleSizePlanService _plans;

    public GetProductBoxPlanEndpoint(IBundleSizePlanService plans) => _plans = plans;

    public override void Configure()
    {
        Get("/commerce/catalog/products/{slug}/box-plan");
        AllowAnonymous();
        Summary(s => s.Summary = "The size plan for a size-tiered bundle product.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);

        var plan = await _plans.GetBySlugAsync(Route<string>("slug") ?? string.Empty, ct);
        if (plan is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(plan, ct);
    }
}
