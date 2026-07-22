namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Resolves the current authenticated principal's primary Party (Spec 072 Y1) — the seam that
/// lets modules honour a customer principal without referencing Platform's identity internals
/// (the same cross-module shape as <see cref="ITenantCurrencyProvider"/>). Null when the request
/// is anonymous or the user has no party link; callers treat null exactly like a guest.
/// </summary>
public interface ICurrentPartyResolver
{
    Task<Guid?> GetCurrentPartyIdAsync(CancellationToken cancellationToken = default);
}
