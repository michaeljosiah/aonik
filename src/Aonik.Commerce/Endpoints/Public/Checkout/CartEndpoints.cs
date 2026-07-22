using Aonik.Commerce.Contracts.Api.Checkout;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public class CreateCartEndpoint : Endpoint<CreateCartRequest, CartDto>
{
    private readonly ICartService _carts;
    public CreateCartEndpoint(ICartService carts) => _carts = carts;

    public override void Configure()
    {
        Post("/commerce/carts");
        AllowAnonymous();
        Summary(s => s.Summary = "Create a cart (party or anonymous guest).");
    }

    public override async Task HandleAsync(CreateCartRequest req, CancellationToken ct)
    {
        // R10 — a party-bound cart authorizes ONLY by an authenticated principal matching
        // BuyerPartyId, and these anonymous routes carry no principal-to-party mapping yet (that
        // arrives with the storefront customer-identity capability). Accepting a party id here
        // would mint a cart its own creator can never read again — reject it loudly instead.
        if (req.BuyerPartyId is not null)
        {
            throw new Aonik.Commerce.Services.Catalog.StorefrontValidationException(
                "Party-bound carts are not available on this route yet; omit buyerPartyId for a guest cart.");
        }

        // Y3 — an authenticated caller's cart is born party-bound; the principal is the ONLY
        // source of the party (the body rejection above stands).
        var principal = await CartRequestAccess.FromAsync(HttpContext, ct);
        var cart = await _carts.CreateCartAsync(new CreateCartCommand(req.Currency, principal.AuthenticatedPartyId), ct);
        await Send.OkAsync(cart, ct);
    }
}

public class GetCartEndpoint : EndpointWithoutRequest<object>
{
    private readonly ICartService _carts;
    private readonly IBoxCartService _boxCarts;

    public GetCartEndpoint(ICartService carts, IBoxCartService boxCarts)
    {
        _carts = carts;
        _boxCarts = boxCarts;
    }

    public override void Configure()
    {
        Get("/commerce/carts/{cartId:guid}");
        AllowAnonymous();
        // K9 — the runtime dispatches CartDto | BoxCartDto by cart kind; publish both success
        // shapes so generated clients see the concrete models instead of an untyped object.
        Description(b => b
            .Produces<CartDto>(200, "application/json")
            .Produces<BoxCartDto>(200, "application/json"));
        Summary(s =>
        {
            s.Summary = "Get a cart. Box sessions return the Spec 068 §7 box + quote payload.";
            s.Response<CartDto>(200, "A generic cart.");
            s.Response<BoxCartDto>(200, "A box session (the §7 box + quote payload).");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var access = await CartRequestAccess.FromAsync(HttpContext, ct);
        var cart = await _carts.GetCartAsync(Route<Guid>("cartId"), access, ct);
        if (cart is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (cart.BoxBundleProductId is not null)
        {
            // Spec 068 §11 — a box cart's GET is the §7 payload (drift-repaired, quoted).
            await Send.OkAsync((object)await _boxCarts.GetAsync(cart.Id, access, ct), ct);
            return;
        }
        await Send.OkAsync((object)cart, ct);
    }
}

public class AddCartItemEndpoint : Endpoint<AddCartItemRequest, CartDto>
{
    private readonly ICartService _carts;
    public AddCartItemEndpoint(ICartService carts) => _carts = carts;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/items");
        AllowAnonymous();
        Summary(s => s.Summary = "Add a simple product line to a cart.");
    }

    public override async Task HandleAsync(AddCartItemRequest req, CancellationToken ct)
    {
        var cart = await _carts.AddItemAsync(
            new AddCartItemCommand(Route<Guid>("cartId"), req.ProductVariantId, req.Quantity),
            await CartRequestAccess.FromAsync(HttpContext, ct), ct);
        await Send.OkAsync(cart, ct);
    }
}

public class AddBundleToCartEndpoint : Endpoint<AddBundleToCartRequest, CartDto>
{
    private readonly ICartService _carts;
    public AddBundleToCartEndpoint(ICartService carts) => _carts = carts;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/bundles");
        AllowAnonymous();
        Summary(s => s.Summary = "Add a build-your-own-box selection as a bundle line.");
    }

    public override async Task HandleAsync(AddBundleToCartRequest req, CancellationToken ct)
    {
        var cart = await _carts.AddBundleAsync(
            new AddBundleToCartCommand(Route<Guid>("cartId"), req.BundleProductId, req.Selection),
            await CartRequestAccess.FromAsync(HttpContext, ct), ct);
        await Send.OkAsync(cart, ct);
    }
}
