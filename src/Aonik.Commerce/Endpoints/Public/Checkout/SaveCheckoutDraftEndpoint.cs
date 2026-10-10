using Aonik.Commerce.Contracts.Api.Checkout;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class SaveCheckoutDraftEndpoint(ICartService carts)
    : Endpoint<SaveCheckoutDraftRequest, CartCheckoutDraftResponse>
{
    public override void Configure()
    {
        Put("/commerce/carts/{cartId:guid}/checkout-draft");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Replace the saved checkout form using the current cart version.";
            s.Description = "Send X-Cart-Version from the latest cart response. Omitted or null sections clear prior values; incomplete forms may be saved.";
        });
    }

    public override async Task HandleAsync(SaveCheckoutDraftRequest req, CancellationToken ct)
    {
        var response = await carts.SaveCheckoutDraftAsync(Route<Guid>("cartId"),
            new CartCheckoutDraftDto(req.Purchaser, req.Address, req.Recipient, req.DeliveryDate,
                req.Notes, req.Gift, req.CreateAccount, req.DiscountCode, req.AcceptedTermsVersion, req.RequestedPoints, req.GiftCardDraft),
            await CartRequestAccess.FromAsync(HttpContext, ct), ct);
        await Send.OkAsync(response, ct);
    }
}
