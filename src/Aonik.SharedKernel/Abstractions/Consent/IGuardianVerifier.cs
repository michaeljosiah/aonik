namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// Verifies that a named adult is who they claim to be, for the purpose of consenting on a child's
/// behalf (Spec 095 §8).
///
/// <para>
/// "Verifiable parental consent" is a term of art with enumerated methods, and a tickbox is not one
/// of them. A system that records agreement without evidencing <em>who agreed</em> has collected a
/// click, not a consent — and the record it leaves is worse than useless, because it looks like
/// compliance in an audit until someone reads the schema.
/// </para>
///
/// <para>
/// One implementation per method, selected by jurisdiction and available instruments — the same
/// factory-by-configuration shape the auth providers use (ADR-007).
/// </para>
/// </summary>
public interface IGuardianVerifier
{
    /// <summary>One of <c>ConsentVerificationMethods</c>. Identifies this implementation.</summary>
    string Method { get; }

    /// <summary>
    /// Whether this method can be attempted for the given party right now — e.g. the
    /// payment-instrument method needs an active mandate to exist. Lets the caller present only
    /// methods that can actually succeed, rather than offering one that will fail.
    /// </summary>
    Task<bool> IsAvailableAsync(
        Guid tenantId,
        Guid guardianPartyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempt verification. Returns an outcome either way: a failure is a result to record, not an
    /// exception to swallow, because a pattern of failed attempts is itself a signal (Spec 095 §13).
    /// </summary>
    Task<GuardianVerificationResult> VerifyAsync(
        Guid tenantId,
        Guid guardianPartyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of one verification attempt.
/// </summary>
/// <param name="Succeeded">Whether the adult was verified.</param>
/// <param name="Method">Which method produced this outcome.</param>
/// <param name="OutcomeRef">
/// A pointer to the <em>outcome</em> — an authorisation id, a check result id. <strong>Never the
/// document, card number or recording itself</strong>: §13 retains outcomes and no evidence.
/// </param>
/// <param name="FailureReason">Why it failed, for support. Never contains the supplied credential.</param>
public sealed record GuardianVerificationResult(
    bool Succeeded,
    string Method,
    string? OutcomeRef = null,
    string? FailureReason = null)
{
    public static GuardianVerificationResult Success(string method, string outcomeRef)
        => new(true, method, outcomeRef);

    public static GuardianVerificationResult Failure(string method, string reason)
        => new(false, method, FailureReason: reason);
}

/// <summary>
/// Selects the verifier to use, by jurisdiction and by what is actually available for this party.
/// </summary>
public interface IGuardianVerifierFactory
{
    /// <summary>
    /// The strongest method available for this party in this jurisdiction, or null when none is —
    /// in which case the caller must not proceed. <strong>Never falls back to "no verification".</strong>
    /// </summary>
    Task<IGuardianVerifier?> ResolveAsync(
        Guid tenantId,
        Guid guardianPartyId,
        ConsentJurisdiction jurisdiction,
        CancellationToken cancellationToken = default);

    /// <summary>Resolve a specific method, for a caller that has already chosen one.</summary>
    IGuardianVerifier? ForMethod(string method);
}
