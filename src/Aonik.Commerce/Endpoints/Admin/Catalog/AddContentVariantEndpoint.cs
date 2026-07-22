using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Author content for one exact combination (Spec 067 §7). Input selection may be
/// partial; it is normalised through Spec 066 and stored complete. Re-authoring a retired
/// combination revives its row.</summary>
public class AddContentVariantEndpoint : Endpoint<UpsertContentVariantRequest, ProductContentVariantDto>
{
    private readonly IProductContentService _content;

    public AddContentVariantEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Post("/commerce/admin/products/{productId:guid}/content-variants");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Author content for one selection combination.");
    }

    public override async Task HandleAsync(UpsertContentVariantRequest req, CancellationToken ct)
    {
        var result = await _content.AddVariantAsync(Route<Guid>("productId"), Map(req), ct);
        await Send.OkAsync(result, ct);
    }

    internal static UpsertContentVariantCommand Map(UpsertContentVariantRequest req) => new(
        req.SelectionJson, req.ServingLabel, req.Kcal, req.ProteinGrams, req.CarbsGrams,
        req.FatGrams, req.FibreGrams, req.SugarsGrams, req.SaltGrams,
        req.Ingredients, req.Allergens, req.HeatingJson);
}
