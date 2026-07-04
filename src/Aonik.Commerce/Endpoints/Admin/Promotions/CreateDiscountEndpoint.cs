using Aonik.Commerce.Services.Promotions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Promotions;

public record CreateDiscountRequest(string Code, string Kind, decimal Value, string? Currency, int? MaxRedemptions, DateTime? ExpiresAt);

public class CreateDiscountEndpoint : Endpoint<CreateDiscountRequest, DiscountDto>
{
    private readonly IDiscountService _discounts;

    public CreateDiscountEndpoint(IDiscountService discounts) => _discounts = discounts;

    public override void Configure()
    {
        Post("/commerce/admin/discounts");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create a discount/coupon (Percentage or FixedAmount).");
    }

    public override async Task HandleAsync(CreateDiscountRequest req, CancellationToken ct)
    {
        var result = await _discounts.CreateAsync(
            new CreateDiscountCommand(req.Code, req.Kind, req.Value, req.Currency, req.MaxRedemptions, req.ExpiresAt), ct);
        await Send.OkAsync(result, ct);
    }
}
