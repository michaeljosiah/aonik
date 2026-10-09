using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Endpoints.Public.Fulfilment;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.GiftCards;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

internal sealed class GetGiftCardOptionsEndpoint(GiftCardPurchasePricing pricing) : EndpointWithoutRequest<GiftCardOptionsDto>
{
    public override void Configure() { Get("/commerce/storefront/gift-cards/options"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync(await pricing.OptionsAsync(ct), ct);
}

public sealed record GiftCardCodeRequest(string Code);
public sealed record GiftCardTenderRequest(string Code, decimal RequestedAmount);

internal sealed class GiftCardCodeValidator : Validator<GiftCardCodeRequest>
{
    public GiftCardCodeValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(128); }
}

internal sealed class GiftCardTenderValidator : Validator<GiftCardTenderRequest>
{
    public GiftCardTenderValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(128);
        RuleFor(x => x.RequestedAmount).GreaterThan(0).LessThanOrEqualTo(999999.99m);
    }
}

internal sealed class ReadGiftCardBalanceEndpoint(IGiftCardService gifts) : Endpoint<GiftCardCodeRequest, GiftCardBalance>
{
    public override void Configure()
    {
        Post("/commerce/storefront/gift-cards/balance"); AllowAnonymous();
        Options(x => x.RequireRateLimiting(GetDeliveryCoverageEndpoint.RateLimitPolicyName));
    }
    public override async Task HandleAsync(GiftCardCodeRequest req, CancellationToken ct)
    {
        if (HttpContext.Request.Query.Count != 0) { await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct); return; }
        var balance = await gifts.GetBalanceAsync(req.Code, ct);
        if (balance == null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(balance, ct);
    }
}

internal sealed class SetGiftCardPurchaseEndpoint(GiftCardCartService carts) : Endpoint<GiftCardPurchaseSelection, GiftCardCartResponse>
{
    public override void Configure() { Put("/commerce/carts/{cartId:guid}/gift-card-purchase"); AllowAnonymous(); }
    public override async Task HandleAsync(GiftCardPurchaseSelection req, CancellationToken ct)
        => await Send.OkAsync(await carts.SetPurchaseAsync(Route<Guid>("cartId"), req,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
}

internal sealed class RemoveGiftCardPurchaseEndpoint(GiftCardCartService carts) : EndpointWithoutRequest<GiftCardCartResponse>
{
    public override void Configure() { Delete("/commerce/carts/{cartId:guid}/gift-card-purchase"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await carts.SetPurchaseAsync(Route<Guid>("cartId"), null,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
}

internal sealed class SetGiftCardTenderEndpoint(GiftCardCartService carts) : Endpoint<GiftCardTenderRequest, GiftCardCartResponse>
{
    public override void Configure()
    {
        Put("/commerce/carts/{cartId:guid}/gift-card-tender"); AllowAnonymous();
        Options(x => x.RequireRateLimiting(GetDeliveryCoverageEndpoint.RateLimitPolicyName));
    }
    public override async Task HandleAsync(GiftCardTenderRequest req, CancellationToken ct)
    {
        if (HttpContext.Request.Query.Count != 0) { await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct); return; }
        await Send.OkAsync(await carts.SetTenderAsync(Route<Guid>("cartId"), req.Code, req.RequestedAmount,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
    }
}

internal sealed class RemoveGiftCardTenderEndpoint(GiftCardCartService carts) : EndpointWithoutRequest<GiftCardCartResponse>
{
    public override void Configure() { Delete("/commerce/carts/{cartId:guid}/gift-card-tender"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await carts.SetTenderAsync(Route<Guid>("cartId"), null, 0m,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
}
