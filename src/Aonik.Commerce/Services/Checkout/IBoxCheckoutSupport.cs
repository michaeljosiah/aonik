using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// What checkout needs from the box machinery (Spec 068 §9), implemented by the same service
/// that owns the cart quote so the two can never disagree. Validation here is the enforcement —
/// the continue gate is advisory UX.
/// </summary>
internal interface IBoxCheckoutSupport
{
    /// <summary>
    /// Re-validate the full box against the live catalogue. Any drift (option remap, line merge,
    /// unavailable line) persists the repair and throws <see cref="BoxCheckoutDriftException"/> —
    /// checkout stops with the refreshed box and creates nothing (A18); resubmitting against the
    /// refreshed state proceeds. An incomplete box rejects (R8). Returns the priced shape checkout
    /// materialises from.
    /// </summary>
    Task<BoxCheckoutShape> PrepareForCheckoutAsync(Entities.Cart.Cart cart, CancellationToken cancellationToken = default);
}

/// <summary>The authoritative, server-computed figures for a box checkout — never client input.</summary>
internal sealed record BoxCheckoutShape(
    decimal GoodsTotal,
    decimal BoxPrice,
    decimal PersonalisationTotal,
    decimal SurchargeTotal,
    decimal DeliveryCharged,
    string BundleSku,
    int Size,
    /// The order item's DetailsJson box envelope (§9).
    string EnvelopeJson,
    /// Each BoxDish line with its freshly priced Spec 066 §12 envelope.
    IReadOnlyList<(CartItem Line, OptionSelectionResult Priced)> Lines);

/// <summary>
/// A18 — checkout found catalogue drift (or an unavailable line): the customer must explicitly
/// review a changed meal or price before money movement begins. Carries the refreshed box the
/// 409 response body is built from.
/// </summary>
public sealed class BoxCheckoutDriftException : Exception
{
    public BoxCheckoutDriftException(BoxCartDto refreshed)
        : base("The box changed since it was last seen; review the refreshed box and resubmit.")
        => Refreshed = refreshed;

    public BoxCartDto Refreshed { get; }
}
