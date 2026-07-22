using Aonik.Commerce.Contracts.Api.Checkout;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

// Spec 068 §11 — the box session routes. All anonymous; access is NOT cart-id possession: every
// operation after create presents the guest token via X-Cart-Token (R10), and any mismatch is a
// 404 indistinguishable from an unknown cart. Every response carries the whole box + the
// authoritative quote, so concurrent tabs self-correct on their next action.

public class CreateBoxCartEndpoint : Endpoint<CreateBoxCartRequest, BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public CreateBoxCartEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Post("/commerce/carts/box");
        AllowAnonymous();
        Summary(s => s.Summary = "Create a box session. The response's cartToken is the ONLY disclosure of the guest token.");
    }

    public override async Task HandleAsync(CreateBoxCartRequest req, CancellationToken ct)
    {
        // R10 — see CreateCartEndpoint: no principal-to-party mapping exists on these anonymous
        // routes yet, and a party-bound cart ignores the guest token by design, so accepting a
        // party id would mint a session its creator can never touch again.
        if (req.BuyerPartyId is not null)
        {
            throw new Aonik.Commerce.Services.Catalog.StorefrontValidationException(
                "Party-bound box sessions are not available on this route yet; omit buyerPartyId.");
        }

        var result = await _boxCarts.CreateAsync(new CreateBoxCartCommand(
            req.BundleProductId,
            req.Size,
            req.FirstLine is { } line
                ? new AddBoxLineCommand(line.ProductVariantId, line.Quantity, line.Personalisation, line.BundleSlotId)
                : null), ct);
        await Send.OkAsync(result, ct);
    }
}

public class ChangeBoxSizeEndpoint : Endpoint<ChangeBoxSizeRequest, BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public ChangeBoxSizeEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Patch("/commerce/carts/{cartId:guid}/size");
        AllowAnonymous();
        Summary(s => s.Summary = "Change the box size (R1/R2). Reprices the container only.");
    }

    public override async Task HandleAsync(ChangeBoxSizeRequest req, CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.ChangeSizeAsync(
            Route<Guid>("cartId"), req.Size, CartRequestAccess.From(HttpContext), ct), ct);
}

public class AddBoxLineEndpoint : Endpoint<AddBoxLineRequest, BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public AddBoxLineEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/lines");
        AllowAnonymous();
        Summary(s => s.Summary = "Add a personalised dish line; identical lines merge (R6).");
    }

    public override async Task HandleAsync(AddBoxLineRequest req, CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.AddLineAsync(
            Route<Guid>("cartId"),
            new AddBoxLineCommand(req.ProductVariantId, req.Quantity, req.Personalisation, req.BundleSlotId),
            CartRequestAccess.From(HttpContext), ct), ct);
}

/// <summary>Spec 071 — add an ordinary retail extra alongside the box (AddOn line: no slot,
/// no capacity, its own retail price).</summary>
public class AddBoxExtraEndpoint : Endpoint<AddBoxExtraRequest, BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public AddBoxExtraEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/extras");
        AllowAnonymous();
        Summary(s => s.Summary = "Add an add-on extra to a box session (Spec 071).");
    }

    public override async Task HandleAsync(AddBoxExtraRequest req, CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.AddExtraLineAsync(
            Route<Guid>("cartId"),
            new AddBoxExtraCommand(req.ProductVariantId, req.Quantity, req.Personalisation),
            CartRequestAccess.From(HttpContext), ct), ct);
}

public class UpdateBoxLineEndpoint : Endpoint<UpdateBoxLineRequest, BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public UpdateBoxLineEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Patch("/commerce/carts/{cartId:guid}/lines/{lineId:guid}");
        AllowAnonymous();
        Summary(s => s.Summary = "Update a line's quantity or personalisation; applyToUnits splits (FR-10.5).");
    }

    public override async Task HandleAsync(UpdateBoxLineRequest req, CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.UpdateLineAsync(
            Route<Guid>("cartId"), Route<Guid>("lineId"),
            new UpdateBoxLineCommand(req.Quantity, req.Personalisation, req.ApplyToUnits),
            CartRequestAccess.From(HttpContext), ct), ct);
}

public class RemoveBoxLineEndpoint : EndpointWithoutRequest<BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public RemoveBoxLineEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Delete("/commerce/carts/{cartId:guid}/lines/{lineId:guid}");
        AllowAnonymous();
        Summary(s => s.Summary = "Remove a line regardless of quantity.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.RemoveLineAsync(
            Route<Guid>("cartId"), Route<Guid>("lineId"), CartRequestAccess.From(HttpContext), ct), ct);
}

public class QuoteBoxCartEndpoint : EndpointWithoutRequest<BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public QuoteBoxCartEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/quote");
        AllowAnonymous();
        Summary(s => s.Summary = "Recompute and return the authoritative quote (no mutation).");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.QuoteAsync(
            Route<Guid>("cartId"), CartRequestAccess.From(HttpContext), ct), ct);
}

public class ContinueBoxCartEndpoint : EndpointWithoutRequest<BoxCartDto>
{
    private readonly IBoxCartService _boxCarts;
    public ContinueBoxCartEndpoint(IBoxCartService boxCarts) => _boxCarts = boxCarts;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/continue");
        AllowAnonymous();
        Summary(s => s.Summary = "The full-box gate (R8): rejects naming the shortfall unless units == size.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _boxCarts.ContinueAsync(
            Route<Guid>("cartId"), CartRequestAccess.From(HttpContext), ct), ct);
}

/// <summary>Generic carts too — closes the Spec 042 gap (the service method existed, the route
/// didn't). Box carts reject here (R11) and use the /lines route instead.</summary>
public class RemoveCartItemEndpoint : EndpointWithoutRequest<CartDto>
{
    private readonly ICartService _carts;
    public RemoveCartItemEndpoint(ICartService carts) => _carts = carts;

    public override void Configure()
    {
        Delete("/commerce/carts/{cartId:guid}/items/{itemId:guid}");
        AllowAnonymous();
        Summary(s => s.Summary = "Remove an item from a generic cart.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _carts.RemoveItemAsync(
            Route<Guid>("cartId"), Route<Guid>("itemId"), CartRequestAccess.From(HttpContext), ct), ct);
}
