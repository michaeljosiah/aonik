using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>"Reviewed, still correct" — clears RequiresReview without editing, re-capturing the
/// block's all-defaults binding (Spec 067 §6).</summary>
public class ConfirmContentReviewEndpoint : EndpointWithoutRequest<ProductContentDto>
{
    private readonly IProductContentService _content;

    public ConfirmContentReviewEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Post("/commerce/admin/products/{productId:guid}/content/confirm-review");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Confirm the default content block still describes the standard preparation.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _content.ConfirmContentReviewAsync(Route<Guid>("productId"), ct), ct);
}
