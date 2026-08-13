namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// The narrow read Platform needs from Finance to verify a guardian by their payment instrument
/// (Spec 095 §8), expressed as a SharedKernel contract because Platform cannot reference Finance.
///
/// <para>
/// Deliberately minimal: it answers "does this party hold a live standing authorisation, and when
/// did they give it" and nothing else. The consent path has no business reading a payment method,
/// a token, or an amount.
/// </para>
/// </summary>
public interface IGuardianMandateReader
{
    /// <summary>
    /// The party's active, unexpired, unrevoked mandate — or null. A mandate is an
    /// <em>interactive</em> authorisation by an identified adult against an instrument a provider
    /// has already verified, which is what makes it evidence rather than an assertion.
    /// </summary>
    Task<GuardianMandateInfo?> GetActiveMandateAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);
}

/// <param name="MandateId">The authorisation being cited as evidence.</param>
/// <param name="AuthorisedAt">When the adult gave it. Always an interactive moment.</param>
/// <param name="Provider">Who holds the vaulted instrument.</param>
public sealed record GuardianMandateInfo(
    Guid MandateId,
    DateTime AuthorisedAt,
    string Provider);
