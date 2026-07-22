using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>The coverage listing (Spec 067 §8): every authored combination plus the
/// single-choice-deviation gaps — authoring gaps made visible instead of silent.</summary>
public class GetContentCoverageEndpoint : EndpointWithoutRequest<ContentCoverageDto>
{
    private readonly IProductContentService _content;

    public GetContentCoverageEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/content-coverage");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List authored content combinations and single-choice gaps.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _content.GetCoverageAsync(Route<Guid>("productId"), ct), ct);
}
