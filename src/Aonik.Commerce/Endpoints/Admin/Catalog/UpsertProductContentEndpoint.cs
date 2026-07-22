using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Upsert the default ("standard preparation") content block. Clears RequiresReview and
/// re-captures which all-defaults combination the block describes (Spec 067 §6).</summary>
public class UpsertProductContentEndpoint : Endpoint<UpsertProductContentRequest, ProductContentDto>
{
    private readonly IProductContentService _content;

    public UpsertProductContentEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/content");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Upsert a product's default content block.");
    }

    public override async Task HandleAsync(UpsertProductContentRequest req, CancellationToken ct)
    {
        var result = await _content.UpsertContentAsync(
            Route<Guid>("productId"),
            new UpsertProductContentCommand(
                req.ServingLabel, req.Kcal, req.ProteinGrams, req.CarbsGrams, req.FatGrams,
                req.FibreGrams, req.SugarsGrams, req.SaltGrams,
                req.Ingredients, req.Allergens, req.HeatingJson),
            ct);
        await Send.OkAsync(result, ct);
    }
}
