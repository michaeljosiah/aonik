using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class UpdateContentVariantEndpoint : Endpoint<UpsertContentVariantRequest, ProductContentVariantDto>
{
    private readonly IProductContentService _content;

    public UpdateContentVariantEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Put("/commerce/admin/content-variants/{variantId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update an authored content variant.");
    }

    public override async Task HandleAsync(UpsertContentVariantRequest req, CancellationToken ct)
    {
        var result = await _content.UpdateVariantAsync(
            Route<Guid>("variantId"),
            AddContentVariantEndpoint.Map(req),
            req.ExpectedCanonicalSelectionJson
                ?? throw new StorefrontValidationException(
                    "V-C11: expectedCanonicalSelectionJson is required when updating a variant — "
                    + "an update must name the combination it is meant to land on."),
            ct);
        await Send.OkAsync(result, ct);
    }
}
