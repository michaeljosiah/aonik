using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>GET /commerce/admin/products/{productId}/content — Spec 075.</summary>
public class GetAdminProductContentEndpoint : EndpointWithoutRequest<AdminProductContentDto>
{
    private readonly IProductContentService _content;

    public GetAdminProductContentEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/content");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The RAW content block + variants + server-computed staleness (never the public resolution).");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _content.GetAdminAsync(Route<Guid>("productId"), ct), ct);
}
