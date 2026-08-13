namespace Aonik.SharedKernel.Abstractions.Safety;

/// <summary>
/// Which external companies this subject's content may be sent to (Spec 096 §16.1).
///
/// <para>
/// Routing classifiers through <c>AiRoutePolicy</c> was the right correction and it opened a hole.
/// Spec 095 §9 requires the consent text to <strong>name</strong> the external companies, and makes
/// that list part of the versioned terms — but <c>IConsentReader</c> checks only subject and purpose.
/// So a routing edit, or an automatic failover, could send a child's content to a company the family
/// had never heard of, with no terms published and no consent re-obtained.
/// </para>
///
/// <para>
/// It is the most likely way this design breaches consent in production, <em>precisely because
/// failover is supposed to be automatic and invisible</em>. This contract is what closes it: route
/// selection is intersected with the answer, and a provider outside it is not a candidate at any
/// priority.
/// </para>
/// </summary>
public interface IConsentedProviderReader
{
    /// <summary>
    /// Provider names named by the subject's <em>active</em> terms version.
    ///
    /// <para>
    /// An empty result means <strong>no provider may be used</strong> — not "no restriction". A
    /// subject with no active grant has consented to nothing, and the permissive reading of that is
    /// the whole failure this exists to prevent.
    /// </para>
    /// </summary>
    Task<IReadOnlySet<string>> GetConsentedProvidersAsync(
        Guid tenantId,
        Guid subjectPartyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when routing would send a subject's content to a provider their terms do not name.
///
/// <para>
/// Deliberately not a silent fallback to another provider: adding one is a terms change, with the
/// publication-time revocation that implies. The generation fails closed and the operator is
/// alerted, which is the awkward-but-correct outcome §16.1 accepts.
/// </para>
/// </summary>
public sealed class ProviderNotConsentedException : Exception
{
    public ProviderNotConsentedException(Guid subjectPartyId, string provider)
        : base($"Provider '{provider}' is not named by the active terms for subject {subjectPartyId}.")
    {
        SubjectPartyId = subjectPartyId;
        Provider = provider;
    }

    public Guid SubjectPartyId { get; }
    public string Provider { get; }
}
