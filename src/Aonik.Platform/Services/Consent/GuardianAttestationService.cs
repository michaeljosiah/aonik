using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Records the out-of-band verifications an operator performs (Spec 095 §8, signed-form method).
///
/// <para>
/// Separate from <c>IConsentService</c> on purpose: this is an <em>operator</em> action, taken by a
/// named member of staff about a form they have physically read, and it should be as visible in the
/// audit log as it is uncomfortable to perform. Folding it into the consent write path would make it
/// look like an ordinary automated step.
/// </para>
/// </summary>
internal interface IGuardianAttestationService
{
    Task<Guid> AttestAsync(
        Guid guardianPartyId,
        Guid attestedByUserId,
        string? evidenceRef,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws an attestation — a form later found to be forged, a carer whose arrangement ended.
    /// Existing consent grants are NOT retroactively revoked here: lawfulness is judged at the time
    /// of processing, so the grant stands and the attestation stops supporting new ones.
    /// </summary>
    Task RevokeAsync(Guid attestationId, string reason, CancellationToken cancellationToken = default);
}

internal sealed class GuardianAttestationService : IGuardianAttestationService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ConsentOptions _options;

    public GuardianAttestationService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        IOptions<ConsentOptions> options)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _options = options.Value;
    }

    public async Task<Guid> AttestAsync(
        Guid guardianPartyId,
        Guid attestedByUserId,
        string? evidenceRef,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (guardianPartyId == Guid.Empty)
        {
            throw new ArgumentException("Guardian party id is required.", nameof(guardianPartyId));
        }

        // A named person, not a role. "Someone in support checked it" is not an attestation, and
        // this argument is what makes it one.
        if (attestedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "An attestation must name the staff member who made it.", nameof(attestedByUserId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var partyExists = await _dbContext.Parties
            .AsNoTracking()
            .AnyAsync(p => p.Id == guardianPartyId && p.TenantId == tenantId, cancellationToken);

        if (!partyExists)
        {
            throw new InvalidOperationException($"Party {guardianPartyId} not found.");
        }

        var attestation = new GuardianAttestation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GuardianPartyId = guardianPartyId,
            Method = ConsentVerificationMethods.SignedForm,
            AttestedByUserId = attestedByUserId,
            EvidenceRef = evidenceRef,
            Notes = notes,
            AttestedAt = now,
            ExpiresAt = now.AddDays(_options.SignedFormAttestationDays)
        };

        _dbContext.GuardianAttestations.Add(attestation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            "GuardianAttestationRecorded",
            nameof(GuardianAttestation),
            attestation.Id,
            tenantId,
            actorId: attestedByUserId,
            correlationId: null,
            detailsJson: null,
            cancellationToken: cancellationToken);

        return attestation.Id;
    }

    public async Task RevokeAsync(
        Guid attestationId, string reason, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var attestation = await _dbContext.GuardianAttestations
            .FirstOrDefaultAsync(a => a.Id == attestationId && a.TenantId == tenantId, cancellationToken);

        if (attestation is null || attestation.RevokedAt is not null)
        {
            return;
        }

        attestation.RevokedAt = _clock.UtcNow;
        attestation.RevocationReason = reason;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            "GuardianAttestationRevoked",
            nameof(GuardianAttestation),
            attestation.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: null,
            cancellationToken: cancellationToken);
    }
}
