using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>
/// Full-replace of a product's option narrowing (Spec 066 §11). Idempotent: posting the same body
/// twice leaves the same state. An empty list makes the product not personalisable.
/// </summary>
public class SetProductOptionGroupsEndpoint : Endpoint<SetProductOptionGroupsRequest, IReadOnlyList<EffectiveOptionGroupDto>>
{
    private readonly IProductOptionService _options;

    public SetProductOptionGroupsEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/option-groups");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Replace which option groups and choices a product offers.");
    }

    public override async Task HandleAsync(SetProductOptionGroupsRequest req, CancellationToken ct)
    {
        var productId = Route<Guid>("productId");

        // A missing or misspelled `groups` property binds to null, and coalescing that to an empty
        // list would make a malformed payload indistinguishable from an intentional clear — quietly
        // removing the product's entire personalisation surface. Clearing has to be said out loud.
        if (req.Groups is null)
        {
            throw new OptionValidationException(
                "V6",
                "A 'groups' array is required. To remove every option group from this product, send an explicit empty array.");
        }

        var lines = req.Groups
            .Select(g => new ProductOptionGroupLine(
                g.GroupKey, g.AllowedChoiceKeys, g.DefaultChoiceKey, g.SelectionModeOverride, g.SortOrder))
            .ToList();

        await _options.SetProductOptionGroupsAsync(productId, new SetProductOptionGroupsCommand(lines), ct);

        // Echo what the product now actually offers, so the operator sees the resolved effect
        // (defaults included) rather than just their own input.
        await Send.OkAsync(await _options.GetEffectiveOptionsAsync(productId, ct), ct);
    }
}
