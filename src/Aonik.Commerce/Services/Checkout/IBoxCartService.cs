using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// The box-building session (Spec 068): a cart with a bundle reference and chosen size, filled
/// line by line with personalised dishes. Every operation authorizes via <see cref="CartAccessContext"/>
/// (R10 — 404 on mismatch, indistinguishable from an unknown cart), applies §8 catalogue-drift
/// handling on load, recomputes prices from scratch (no client-supplied amount is read anywhere),
/// and returns the whole box + the authoritative quote so concurrent tabs self-correct.
/// </summary>
public interface IBoxCartService
{
    /// <summary>Create a box session — the only operation that discloses the guest cart token.</summary>
    Task<BoxCartDto> CreateAsync(CreateBoxCartCommand command, CancellationToken cancellationToken = default);

    Task<BoxCartDto> GetAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>R1/R2 — validate against the plan; reject below current units naming the count to
    /// remove. Reprices the container only.</summary>
    Task<BoxCartDto> ChangeSizeAsync(Guid cartId, int newSize, CartAccessContext access, CancellationToken cancellationToken = default);

    Task<BoxCartDto> AddLineAsync(Guid cartId, AddBoxLineCommand command, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>Spec 071 — add an AddOn line: an ordinary retail product alongside the box,
    /// consuming no box space, priced at its own retail price (mandatory in the cart currency).</summary>
    Task<BoxCartDto> AddExtraLineAsync(Guid cartId, AddBoxExtraCommand command, CartAccessContext access, CancellationToken cancellationToken = default);

    Task<BoxCartDto> UpdateLineAsync(Guid cartId, Guid lineId, UpdateBoxLineCommand command, CartAccessContext access, CancellationToken cancellationToken = default);

    Task<BoxCartDto> RemoveLineAsync(Guid cartId, Guid lineId, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>Recompute and return — no mutation beyond §8 drift repair.</summary>
    Task<BoxCartDto> QuoteAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>R8 — the full-box gate: rejects naming the shortfall unless units == size and no
    /// line is flagged unavailable. Stateless; checkout independently re-validates.</summary>
    Task<BoxCartDto> ContinueAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);
}
