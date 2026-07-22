using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Soft-retire a content variant (Spec 067 V-C5) — rows remain for history, audit and
/// reactivation; nothing content-related hard-deletes.</summary>
public class DeactivateContentVariantEndpoint : EndpointWithoutRequest
{
    private readonly IProductContentService _content;

    public DeactivateContentVariantEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Delete("/commerce/admin/content-variants/{variantId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Retire an authored content variant (soft).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _content.DeactivateVariantAsync(Route<Guid>("variantId"), ct);
        await Send.NoContentAsync(ct);
    }
}
