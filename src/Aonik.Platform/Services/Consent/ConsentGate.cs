using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §12.1. The single enforcement point, and it fails closed everywhere.
/// </summary>
internal sealed class ConsentGate : IConsentGate
{
    private readonly IConsentReader _consentReader;
    private readonly IGuardianshipReader _guardianshipReader;
    private readonly ITenantProvider _tenantProvider;

    public ConsentGate(
        IConsentReader consentReader,
        IGuardianshipReader guardianshipReader,
        ITenantProvider tenantProvider)
    {
        _consentReader = consentReader;
        _guardianshipReader = guardianshipReader;
        _tenantProvider = tenantProvider;
    }

    public async Task EnsureAsync(
        Guid subjectPartyId, string purpose, CancellationToken cancellationToken = default)
    {
        // An empty subject is a caller bug, and the safe reading of a bug is refusal. Treating it as
        // "no subject, therefore no restriction" is how a gate becomes a no-op under a null.
        if (subjectPartyId == Guid.Empty || string.IsNullOrWhiteSpace(purpose))
        {
            throw new ConsentRequiredException(subjectPartyId, purpose ?? "(none)");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (!await _consentReader.HasConsentAsync(tenantId, subjectPartyId, purpose, cancellationToken))
        {
            throw new ConsentRequiredException(subjectPartyId, purpose);
        }
    }

    public async Task EnsureCanActForAsync(
        Guid callerPartyId, Guid subjectPartyId, CancellationToken cancellationToken = default)
    {
        if (callerPartyId == Guid.Empty || subjectPartyId == Guid.Empty)
        {
            throw new GuardianAuthorityRequiredException(callerPartyId, subjectPartyId);
        }

        // Acting for yourself is always permitted, and is what makes the age-up transition work: at
        // ConsentAgeOn the guardian edge is still active but the young person is the one deciding.
        if (callerPartyId == subjectPartyId)
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (!await _guardianshipReader.HasAuthorityAsync(
                tenantId, callerPartyId, subjectPartyId, cancellationToken))
        {
            throw new GuardianAuthorityRequiredException(callerPartyId, subjectPartyId);
        }
    }

    public async Task EnsureGenerationAsync(
        Guid subjectPartyId, GenerationRoute route, CancellationToken cancellationToken = default)
    {
        // service-core is required on every route: it is what makes the account exist at all.
        await EnsureAsync(subjectPartyId, ConsentPurposes.ServiceCore, cancellationToken);

        // The ROUTE decides the rest. Resolving it first is the whole point of §12.3 — the question
        // consent was actually obtained about is whether this child's words leave the device, not
        // whether a generation happened.
        foreach (var purpose in route.RequiredPurposes())
        {
            await EnsureAsync(subjectPartyId, purpose, cancellationToken);
        }
    }
}
