using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

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
