namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// "Is there an active, unexpired grant for this subject and purpose?" (Spec 095 §12.1).
///
/// <para>
/// Deliberately version-agnostic: callers ask about subject and purpose only. Keeping the terms
/// version out of the question is what lets a material terms change invalidate consent centrally,
/// rather than requiring every caller to know which version is current.
/// </para>
///
/// <para>
/// This reader never consults the legacy archive. Consent obtained before any verification existed
/// authorises nothing.
/// </para>
/// </summary>
public interface IConsentReader
{
    /// <summary>
    /// Fails closed: an absent, expired or revoked grant returns false. Never "log and continue".
    /// </summary>
    Task<bool> HasConsentAsync(
        Guid tenantId,
        Guid subjectPartyId,
        string purpose,
        CancellationToken cancellationToken = default);

    /// <summary>Purposes currently granted for this subject. For presentation, not authorisation.</summary>
    Task<IReadOnlyList<string>> GetGrantedPurposesAsync(
        Guid tenantId,
        Guid subjectPartyId,
        CancellationToken cancellationToken = default);
}
