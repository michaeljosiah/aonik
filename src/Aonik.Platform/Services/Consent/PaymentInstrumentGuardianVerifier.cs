using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.Platform.Entities.Party;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §8, primary method. Verifies a guardian through a standing payment authorisation they
/// already hold.
///
/// <para>
/// This is the happy path and should stay the default, for a reason worth stating: the subscribing
/// parent already has a mandate (Spec 087), so <strong>the strongest available verification method
/// is a by-product of paying</strong> — no extra step, no document upload, no friction added to the
/// families most likely to be doing the right thing.
/// </para>
///
/// <para>
/// What makes it evidence rather than an assertion: a mandate is an <em>interactive</em>
/// authorisation, given by an identified adult, against an instrument a regulated provider has
/// already verified. We cite the authorisation; we never hold the instrument.
/// </para>
/// </summary>
internal sealed class PaymentInstrumentGuardianVerifier : IGuardianVerifier
{
    private readonly IGuardianMandateReader _mandateReader;
    private readonly ILogger<PaymentInstrumentGuardianVerifier> _logger;

    public PaymentInstrumentGuardianVerifier(
        IGuardianMandateReader mandateReader,
        ILogger<PaymentInstrumentGuardianVerifier> logger)
    {
        _mandateReader = mandateReader;
        _logger = logger;
    }

    public string Method => ConsentVerificationMethods.PaymentInstrument;

    public async Task<bool> IsAvailableAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
        => await _mandateReader.GetActiveMandateAsync(tenantId, guardianPartyId, cancellationToken) is not null;

    public async Task<GuardianVerificationResult> VerifyAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
    {
        var mandate = await _mandateReader.GetActiveMandateAsync(tenantId, guardianPartyId, cancellationToken);

        if (mandate is null)
        {
            // No mandate is an ordinary outcome, not an error: a £0-tier guardian has none, and the
            // factory will offer them a fallback method instead.
            return GuardianVerificationResult.Failure(
                Method, "No active payment mandate for this party.");
        }

        // The OutcomeRef is the mandate id — a pointer to an authorisation we can re-examine, not
        // the instrument itself. Spec 095 §13 retains outcomes and no evidence: no card number, no
        // token, no document.
        _logger.LogInformation(
            "Guardian {PartyId} verified by payment mandate {MandateId} ({Provider}), authorised {AuthorisedAt:O}.",
            guardianPartyId, mandate.MandateId, mandate.Provider, mandate.AuthorisedAt);

        return GuardianVerificationResult.Success(Method, mandate.MandateId.ToString());
    }
}
