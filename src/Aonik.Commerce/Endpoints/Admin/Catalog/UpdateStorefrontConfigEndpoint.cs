using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Writes the Commerce.Storefront.* settings under AdminWritePolicy (Spec 070 §9/§10):
/// the platform tenant-settings endpoint requires AdminPolicy, which would let an Operations user
/// edit every product yet not the storefront settings — same store, Commerce-appropriate policy.</summary>
public class UpdateStorefrontConfigEndpoint : Endpoint<UpdateStorefrontConfigRequest, StorefrontConfigDto>
{
    private readonly IStorefrontConfigService _config;

    public UpdateStorefrontConfigEndpoint(IStorefrontConfigService config) => _config = config;

    public override void Configure()
    {
        Put("/commerce/admin/storefront-config");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update storefront configuration. Omitted members are unchanged; empty strings clear.");
    }

    public override async Task HandleAsync(UpdateStorefrontConfigRequest req, CancellationToken ct)
    {
        var result = await _config.UpdateAsync(
            new UpdateStorefrontConfigCommand(
                req.RecommendedChoiceLabel,
                req.ResultsPageSize,
                req.BackToTopTriggerJson,
                req.DeliveryListAmount,
                req.DeliveryChargedAmount,
                req.DefaultBoxSlug,
                req.ExtrasCollectionSlug),
            ct);
        await Send.OkAsync(result, ct);
    }
}
