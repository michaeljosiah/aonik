using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class SetPriceEndpoint : Endpoint<SetPriceRequest, ProductPriceDto>
{
    private readonly IProductPricingService _pricing;

    public SetPriceEndpoint(IProductPricingService pricing) => _pricing = pricing;

    public override void Configure()
    {
        Post("/commerce/admin/variants/{variantId:guid}/prices");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set the active price for a variant in a currency.");
    }

    public override async Task HandleAsync(SetPriceRequest req, CancellationToken ct)
    {
        var variantId = Route<Guid>("variantId");
        var result = await _pricing.SetPriceAsync(
            new SetPriceCommand(variantId, req.Currency, req.Amount, req.EffectiveFrom, req.EffectiveTo), ct);
        await Send.OkAsync(result, ct);
    }
}
