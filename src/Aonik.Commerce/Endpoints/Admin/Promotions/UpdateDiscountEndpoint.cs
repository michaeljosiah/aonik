using System.Text.Json.Serialization;

using Aonik.Commerce.Services.Promotions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Promotions;

public record UpdateDiscountRequest(string Kind, decimal Value, [property: JsonRequired] bool IsActive,
    [property: JsonRequired] string? Currency, [property: JsonRequired] int? MaxRedemptions,
    [property: JsonRequired] DateTime? ExpiresAt, [property: JsonRequired] IReadOnlyList<Guid>? EligibleProductIds,
    string ExpectedVersion);

public sealed class UpdateDiscountEndpoint(IDiscountService discounts) : Endpoint<UpdateDiscountRequest, DiscountDto>
{
    public override void Configure()
    {
        Put("/commerce/admin/discounts/{discountId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Replace a discount definition using its version. The code is immutable; null optional values clear them.");
    }

    public override async Task HandleAsync(UpdateDiscountRequest req, CancellationToken ct)
        => await Send.OkAsync(await discounts.UpdateAsync(Route<Guid>("discountId"), new UpdateDiscountCommand(
            req.Kind, req.Value, req.IsActive, req.Currency, req.MaxRedemptions, req.ExpiresAt,
            req.EligibleProductIds, req.ExpectedVersion), ct), ct);
}
