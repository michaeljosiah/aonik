using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §8, government-ID fallback — for guardians on a £0 tier with no payment instrument.
///
/// <para>
/// <strong>Disabled by default, and that is not caution — it is correctness.</strong>
/// <c>ComplianceService.ScreenPartyAsync</c> is currently a stub: it logs
/// <em>"Compliance screening is a stub — always returns Passed"</em> and does exactly that, and no
/// document-verification provider exists anywhere in the solution.
/// </para>
///
/// <para>
/// So a verifier built naively on it would verify <strong>every</strong> guardian who asked,
/// automatically. That is worse than not offering the method at all, because it would produce a
/// consent record carrying <c>government-id</c> — evidence, in an audit, of a check that never
/// happened. Unverified consent that looks verified is the single failure this whole specification
/// exists to prevent.
/// </para>
///
/// <para>
/// The shape is therefore built and the switch left off. Turning it on is an explicit operator act
/// that must follow wiring a real provider, and <see cref="ConsentOptions"/> says so where someone
/// would otherwise flip it.
/// </para>
/// </summary>
internal sealed class GovernmentIdGuardianVerifier : IGuardianVerifier
{
    private const string ScreeningCheckType = "GuardianIdentity";

    private readonly IComplianceService _compliance;
    private readonly ConsentOptions _options;
    private readonly ILogger<GovernmentIdGuardianVerifier> _logger;

    public GovernmentIdGuardianVerifier(
        IComplianceService compliance,
        IOptions<ConsentOptions> options,
        ILogger<GovernmentIdGuardianVerifier> logger)
    {
        _compliance = compliance;
        _options = options.Value;
        _logger = logger;
    }

    public string Method => ConsentVerificationMethods.GovernmentId;

    public Task<bool> IsAvailableAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
        => Task.FromResult(_options.GovernmentIdVerification.Enabled);

    public async Task<GuardianVerificationResult> VerifyAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
    {
        if (!_options.GovernmentIdVerification.Enabled)
        {
            // Fails closed rather than falling through. A caller that reached here despite
            // IsAvailableAsync saying no is a bug, and the safe reading of a bug is refusal.
            return GuardianVerificationResult.Failure(
                Method,
                "Government-ID verification is not enabled: no document-verification provider is configured.");
        }

        var screening = await _compliance.ScreenPartyAsync(
            guardianPartyId, ScreeningCheckType, cancellationToken);

        var passed = string.Equals(screening.ResultStatus, "Passed", StringComparison.OrdinalIgnoreCase);

        if (!passed)
        {
            return GuardianVerificationResult.Failure(
                Method, $"Identity check did not pass: {screening.ResultStatus}.");
        }

        _logger.LogInformation(
            "Guardian {PartyId} verified by identity check {CheckId}.",
            guardianPartyId, screening.ScreeningCheckId);

        // The check id, not the document. §13 retains outcomes and no evidence — no image, no
        // number, nothing that would make this record a liability of its own.
        return GuardianVerificationResult.Success(Method, screening.ScreeningCheckId.ToString());
    }
}

/// <summary>
/// Spec 095 §8, signed-form fallback. Slow, high-friction, and genuinely the right answer for the
/// cases the other methods cannot reach — an institutional carer, a kinship carer with no card.
///
/// <para>
/// This verifier does <strong>not</strong> perform a check. The signed-form method is inherently
/// manual: a form is returned, a human reads it and matches it to a named adult. What the platform
/// does is hold the evidence that this happened and answer whether it is still current. A verifier
/// that tried to automate it would be inventing an outcome, which is the failure mode the
/// government-ID verifier above is switched off to avoid.
/// </para>
/// </summary>
internal sealed class SignedFormGuardianVerifier : IGuardianVerifier
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public SignedFormGuardianVerifier(
        PlatformDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public string Method => ConsentVerificationMethods.SignedForm;

    public async Task<bool> IsAvailableAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
        => await CurrentAttestationAsync(tenantId, guardianPartyId, cancellationToken) is not null;

    public async Task<GuardianVerificationResult> VerifyAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
    {
        var attestation = await CurrentAttestationAsync(tenantId, guardianPartyId, cancellationToken);

        if (attestation is null)
        {
            // The ordinary outcome when the form has not arrived yet, or has expired. Not an error:
            // this method is asynchronous by nature and the caller is expected to wait.
            return GuardianVerificationResult.Failure(
                Method, "No current signed-form attestation for this party.");
        }

        return GuardianVerificationResult.Success(Method, attestation.Id.ToString());
    }

    private async Task<GuardianAttestation?> CurrentAttestationAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        return await _dbContext.GuardianAttestations
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId
                && a.GuardianPartyId == guardianPartyId
                && a.Method == ConsentVerificationMethods.SignedForm
                && a.RevokedAt == null
                && a.ExpiresAt > now)
            .OrderByDescending(a => a.AttestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
