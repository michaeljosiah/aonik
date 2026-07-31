namespace Aonik.SharedKernel.Abstractions.Ledgers;

/// <summary>
/// Resolves which ledger a cross-module caller should post to (Spec 088 §5.1). Implemented by
/// <c>Aonik.Finance</c>.
///
/// Most consumers have exactly one ledger and no opinion about it; this saves them from carrying a
/// <c>LedgerId</c> they cannot meaningfully choose. Consumers that genuinely post to several
/// ledgers should track the id themselves and skip this.
/// </summary>
public interface ILedgerResolver
{
    /// <summary>
    /// The current tenant's canonical ledger. A tenant with exactly one ledger resolves to it; a
    /// tenant with several must have one marked canonical.
    /// </summary>
    /// <remarks>
    /// <b>Throws rather than guessing.</b> Picking a ledger arbitrarily when a tenant has several
    /// and none is marked would post financially material entries into an unpredictable ledger,
    /// with an unpredictable base currency — a silent, compounding error that is far worse than a
    /// startup failure. Which ledger is canonical is an operator's decision, not a default a
    /// resolver may invent.
    /// </remarks>
    /// <exception cref="InvalidStateException">The tenant has no ledger, or several with none marked canonical.</exception>
    Task<Guid> GetCanonicalLedgerIdAsync(CancellationToken cancellationToken = default);
}
