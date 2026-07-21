using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>
/// Full-replace of a product's option narrowing (Spec 066 §11). Idempotent: posting the same body
/// twice leaves the same state. An empty list makes the product not personalisable.
/// </summary>
public class SetProductOptionGroupsEndpoint : Endpoint<SetProductOptionGroupsRequest, IReadOnlyList<EffectiveOptionGroupDto>>
{
    private readonly IProductOptionService _options;

    public SetProductOptionGroupsEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/option-groups");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Replace which option groups and choices a product offers.");
    }

    public override async Task HandleAsync(SetProductOptionGroupsRequest req, CancellationToken ct)
    {
        var productId = Route<Guid>("productId");

        var lines = (req.Groups ?? [])
            .Select(g => new ProductOptionGroupLine(
                g.GroupKey, g.AllowedChoiceKeys, g.DefaultChoiceKey, g.SelectionModeOverride, g.SortOrder))
            .ToList();

        await _options.SetProductOptionGroupsAsync(productId, new SetProductOptionGroupsCommand(lines), ct);

        // Echo what the product now actually offers, so the operator sees the resolved effect
        // (defaults included) rather than just their own input.
        await Send.OkAsync(await _options.GetEffectiveOptionsAsync(productId, ct), ct);
    }
}

/// <summary>Sets or clears a product's per-unit surcharge (Spec 066 §11).</summary>
public class SetUnitSurchargeEndpoint : Endpoint<SetUnitSurchargeRequest, ProductDto>
{
    private readonly IProductOptionService _options;
    private readonly IProductService _products;

    public SetUnitSurchargeEndpoint(IProductOptionService options, IProductService products)
    {
        _options = options;
        _products = products;
    }

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/surcharge");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set or clear a product's per-unit surcharge and its currency.");
    }

    public override async Task HandleAsync(SetUnitSurchargeRequest req, CancellationToken ct)
    {
        var productId = Route<Guid>("productId");
        await _options.SetUnitSurchargeAsync(productId, new SetUnitSurchargeCommand(req.Amount, req.Currency), ct);

        var product = await _products.GetProductAsync(productId, ct);
        if (product is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(product, ct);
    }
}
