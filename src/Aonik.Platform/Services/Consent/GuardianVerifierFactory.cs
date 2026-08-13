using Aonik.SharedKernel.Abstractions.Consent;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §8. Selects the strongest verification method that is both accepted in the jurisdiction
/// and actually available for this party — the same factory-by-configuration shape the auth
/// providers use (ADR-007).
/// </summary>
internal sealed class GuardianVerifierFactory : IGuardianVerifierFactory
{
    private readonly IReadOnlyDictionary<string, IGuardianVerifier> _verifiers;

    public GuardianVerifierFactory(IEnumerable<IGuardianVerifier> verifiers)
    {
        _verifiers = verifiers.ToDictionary(v => v.Method, StringComparer.OrdinalIgnoreCase);
    }

    public IGuardianVerifier? ForMethod(string method)
        => !string.IsNullOrWhiteSpace(method) && _verifiers.TryGetValue(method, out var verifier)
            ? verifier
            : null;

    public async Task<IGuardianVerifier?> ResolveAsync(
        Guid tenantId,
        Guid guardianPartyId,
        ConsentJurisdiction jurisdiction,
        CancellationToken cancellationToken = default)
    {
        // AcceptedMethods is ordered strongest-first, and the jurisdiction decides what counts —
        // so a method we have implemented but which is not accepted here is simply not a candidate.
        foreach (var method in jurisdiction.AcceptedMethods)
        {
            if (!_verifiers.TryGetValue(method, out var verifier))
            {
                continue;
            }

            if (await verifier.IsAvailableAsync(tenantId, guardianPartyId, cancellationToken))
            {
                return verifier;
            }
        }

        // Returning null is the correct answer and the caller must not proceed. There is deliberately
        // no "unverified" fallback: consent obtained without verification is not consent, and a
        // permissive default here would quietly undo the whole specification.
        return null;
    }
}
