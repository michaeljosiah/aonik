using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>GET /commerce/admin/products/{productId}/option-groups — Spec 074.</summary>
public class GetProductNarrowingEndpoint : EndpointWithoutRequest<IReadOnlyList<ProductNarrowingLineDto>>
{
    private readonly IProductOptionService _options;

    public GetProductNarrowingEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/option-groups");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The product's STORED option narrowing (raw lines, null allowed-keys preserved).");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _options.GetNarrowingAsync(Route<Guid>("productId"), ct), ct);
}
