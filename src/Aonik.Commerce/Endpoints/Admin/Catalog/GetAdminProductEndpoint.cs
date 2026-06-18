using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class GetAdminProductEndpoint : EndpointWithoutRequest<ProductDto>
{
    private readonly IProductService _products;

    public GetAdminProductEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get full product detail (variants, prices, media, bundle slots).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var productId = Route<Guid>("productId");
        var result = await _products.GetProductAsync(productId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
