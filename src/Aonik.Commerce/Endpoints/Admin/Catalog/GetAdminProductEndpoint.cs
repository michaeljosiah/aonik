using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Spec 070 §7 — deliberately a DIFFERENT contract from the public product read: the
/// admin detail includes the hidden search keywords, which appear in no public response. Sharing
/// one DTO would either disclose them publicly or blind the editor into erasing them.</summary>
public class GetAdminProductEndpoint : EndpointWithoutRequest<AdminProductDetailDto>
{
    private readonly IProductService _products;

    public GetAdminProductEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get full product detail including hidden search keywords.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var productId = Route<Guid>("productId");
        var result = await _products.GetAdminProductAsync(productId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
