using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 G3. The write side of consent, and the only service permitted to create a
/// <c>Guardian</c> relationship.
/// </summary>
internal sealed class ConsentService : IConsentService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IConsentJurisdictionResolver _jurisdictionResolver;
    private readonly IGuardianVerifierFactory _verifierFactory;
    private readonly IGuardianVerificationRecorder _verificationRecorder;
    private readonly IGuardianshipReader _guardianshipReader;

    public ConsentService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IConsentJurisdictionResolver jurisdictionResolver,
        IGuardianVerifierFactory verifierFactory,
        IGuardianVerificationRecorder verificationRecorder,
        IGuardianshipReader guardianshipReader)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _jurisdictionResolver = jurisdictionResolver;
        _verifierFactory = verifierFactory;
        _verificationRecorder = verificationRecorder;
        _guardianshipReader = guardianshipReader;
    }

    public async Task<EnrolChildResult> EnrolChildAsync(
        EnrolChildRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;
        var jurisdiction = _jurisdictionResolver.Resolve(request.Jurisdiction);

        // Minted BEFORE verification: it correlates the verification record — which commits on its
        // own scope and survives a rollback — to the child this attempt may or may not create.
        var attemptId = Guid.NewGuid();

        var verificationMethod = await VerifyGuardianAsync(
            tenantId, request.GuardianPartyId, jurisdiction, attemptId, cancellationToken);

        // An exact date is required. §6 removed the fallback because no single default date is safe
        // for all four boundaries: they want opposite conservatism, so any guess is wrong somewhere.
        if (request.ChildDateOfBirth == default)
        {
            throw new ArgumentException(
                "An attested date of birth is required to enrol a child; there is no fallback.",
                nameof(request.ChildDateOfBirth));
        }

        var boundaries = AgeBoundaryCalculator.Compute(request.ChildDateOfBirth, jurisdiction, now);
        var purposes = NormalisePurposes(request.Purposes);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var child = new PartyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyType = "Person",
            Status = "Active",
            DisplayName = request.ChildDisplayName,
            BirthYear = boundaries.BirthYear,
            ConsentBand = boundaries.ConsentBand,
            SafetyBand = boundaries.SafetyBand,
            ConsentAgeOn = boundaries.ConsentAgeOn,
            MajorityOn = boundaries.MajorityOn,
            SafetyBandChangesOn = boundaries.SafetyBandChangesOn
        };
        _dbContext.Parties.Add(child);

        // The ONLY place a Guardian edge is created alongside a new child. PartyService refuses this
        // code outright (§7.2), so there is no other route to it.
        _dbContext.PartyRelationships.Add(new PartyRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromPartyId = request.GuardianPartyId,
            ToPartyId = child.Id,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian,
            IsActive = true
        });

        foreach (var purpose in purposes)
        {
            _dbContext.ConsentGrants.Add(NewGrant(
                tenantId, child.Id, request.GuardianPartyId, purpose,
                request.TermsVersion, jurisdiction.Code, verificationMethod, null, now));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // ── Deviation from Spec 095 §12.2, recorded rather than hidden ──────────────────────────
        //
        // §12.2's pseudocode lists a fourth step inside this transaction: "add the child to the
        // family Group (Spec 086)". That is NOT done here, and cannot be as things stand.
        //
        // Groups is a middle-layer module with its own DbContext, and Platform does not reference it
        // (ADR-005). Two contexts are two connections and therefore two transactions, so calling
        // IGroupService from inside this block would produce a commit that LOOKS atomic and is not —
        // which is worse than not claiming it, because a partial failure would leave a child with a
        // guardian and consent but no family, and nothing would say so.
        //
        // Spec 086 solved exactly this shape for the personal-finance lifecycle contributor, with
        // IGroupDataContext: the group entities are written through the CONSUMING module's context,
        // so one change tracker and one SaveChanges cover both. Applying that pattern here is the
        // correct fix and is a follow-up, not a line of code.
        //
        // Until then the caller adds the child to the family as a second step and compensates on
        // failure. Enrolment is idempotent enough to retry: a child with no group membership is
        // visible and repairable, whereas a group membership for a child who was never enrolled is
        // not creatable at all, so the ordering here is the safe one.
        return new EnrolChildResult(
            child.Id, attemptId, verificationMethod,
            boundaries.ConsentAgeOn, boundaries.MajorityOn, boundaries.SafetyBand);
    }

    public async Task AddGuardianAsync(
        AddGuardianRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var jurisdiction = _jurisdictionResolver.Resolve(request.Jurisdiction);

        // Authority cannot be granted to oneself: an EXISTING active guardian must authorise it.
        // Without this, anyone who could call the API could make themselves a guardian.
        if (request.AuthorisedByPartyId == request.NewGuardianPartyId)
        {
            throw new InvalidOperationException(
                "A guardian cannot authorise their own addition.");
        }

        if (!await _guardianshipReader.HasAuthorityAsync(
                tenantId, request.AuthorisedByPartyId, request.ChildPartyId, cancellationToken))
        {
            throw new GuardianAuthorityRequiredException(request.AuthorisedByPartyId, request.ChildPartyId);
        }

        if (await _guardianshipReader.HasAuthorityAsync(
                tenantId, request.NewGuardianPartyId, request.ChildPartyId, cancellationToken))
        {
            return; // Idempotent: already a guardian.
        }

        // The new guardian is verified to the SAME standard as the first. A second guardian added on
        // a weaker basis would make the strength of the first meaningless.
        await VerifyGuardianAsync(
            tenantId, request.NewGuardianPartyId, jurisdiction, Guid.NewGuid(), cancellationToken);

        _dbContext.PartyRelationships.Add(new PartyRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromPartyId = request.NewGuardianPartyId,
            ToPartyId = request.ChildPartyId,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian,
            IsActive = true,
            Notes = $"Authorised by {request.AuthorisedByPartyId}."
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantAsync(GrantConsentRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        ValidatePurpose(request.Purpose);
        ValidateGrantorAndMethod(request);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Supersede is ATOMIC with the new grant. If it were not, a window would exist in which
        // either two active grants coexist — and the version-agnostic reader would find the stale
        // one — or none does, and processing stops for a subject who is mid-re-consent.
        var existing = await _dbContext.ConsentGrants
            .Where(g => g.TenantId == tenantId
                && g.SubjectPartyId == request.SubjectPartyId
                && g.Purpose == request.Purpose
                && g.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var grant in existing)
        {
            if (grant.TermsVersion == request.TermsVersion)
            {
                // Same version re-granted: idempotent, not a duplicate.
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            grant.RevokedAt = now;
            grant.RevokedByPartyId = request.GrantedByPartyId;
            grant.RevocationReason = ConsentRevocationReasons.TermsSuperseded;
        }

        _dbContext.ConsentGrants.Add(NewGrant(
            tenantId, request.SubjectPartyId, request.GrantedByPartyId, request.Purpose,
            request.TermsVersion, _jurisdictionResolver.Resolve(request.Jurisdiction).Code,
            request.VerificationMethod, request.VerificationRef, now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task WithdrawAsync(WithdrawConsentRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        // Withdrawal-wins (§7.1). NO check that the withdrawing guardian is the one who granted, and
        // NO check that other guardians agree — any single active guardian may withdraw, and it takes
        // effect regardless of the others.
        //
        // Asymmetric on purpose: processing a child's data over an objection from someone with legal
        // authority is the worse error, and the platform is not equipped to adjudicate a family
        // dispute. It takes the conservative branch and leaves the dispute to the people who can
        // actually resolve it.
        var isGuardian = await _guardianshipReader.HasAuthorityAsync(
            tenantId, request.WithdrawnByPartyId, request.SubjectPartyId, cancellationToken);

        var isSelf = request.WithdrawnByPartyId == request.SubjectPartyId;

        if (!isGuardian && !isSelf)
        {
            throw new GuardianAuthorityRequiredException(request.WithdrawnByPartyId, request.SubjectPartyId);
        }

        var grants = await _dbContext.ConsentGrants
            .Where(g => g.TenantId == tenantId
                && g.SubjectPartyId == request.SubjectPartyId
                && g.Purpose == request.Purpose
                && g.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var grant in grants)
        {
            grant.RevokedAt = now;
            grant.RevokedByPartyId = request.WithdrawnByPartyId;
            grant.RevocationReason = ConsentRevocationReasons.Withdrawn;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PublishTermsVersionAsync(
        PublishTermsRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;
        var affected = request.AffectedPurposes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (affected.Count == 0)
        {
            return 0;
        }

        // Revokes at PUBLICATION, not on reply. Supersede-on-grant alone fires only when a guardian
        // responds — so one who never answers would go on authorising processing under terms this
        // spec calls invalid, indefinitely, which is the state re-consent exists to prevent.
        //
        // The consequence is deliberate: processing stops for every affected family until they
        // re-consent. Making that cheap would make consent meaningless.
        var stale = await _dbContext.ConsentGrants
            .Where(g => g.TenantId == tenantId
                && g.RevokedAt == null
                && g.TermsVersion != request.TermsVersion
                && affected.Contains(g.Purpose))
            .ToListAsync(cancellationToken);

        foreach (var grant in stale)
        {
            grant.RevokedAt = now;
            grant.RevocationReason = ConsentRevocationReasons.TermsSuperseded;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private async Task<string> VerifyGuardianAsync(
        Guid tenantId,
        Guid guardianPartyId,
        ConsentJurisdiction jurisdiction,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var verifier = await _verifierFactory.ResolveAsync(
            tenantId, guardianPartyId, jurisdiction, cancellationToken);

        if (verifier is null)
        {
            var unavailable = GuardianVerificationResult.Failure(
                "none", "No accepted verification method is available for this party.");
            await _verificationRecorder.RecordAsync(guardianPartyId, attemptId, unavailable, cancellationToken);
            throw new GuardianVerificationFailedException(guardianPartyId, unavailable.FailureReason!);
        }

        var result = await verifier.VerifyAsync(tenantId, guardianPartyId, cancellationToken);

        // Recorded BEFORE the enrolment transaction opens, and on its own scope, so a failure
        // survives the rollback. §13 keeps one row per attempt including failures precisely so
        // repeated failures are visible — rolling that back destroys the signal in the case it
        // exists for.
        await _verificationRecorder.RecordAsync(guardianPartyId, attemptId, result, cancellationToken);

        if (!result.Succeeded)
        {
            throw new GuardianVerificationFailedException(
                guardianPartyId, result.FailureReason ?? "Verification failed.");
        }

        return result.Method;
    }

    /// <summary>
    /// service-core is always present; everything else defaults to NOT granted (§10.1). A parent who
    /// has not been asked has not agreed, and pre-ticked boxes are the canonical example of consent
    /// that is not freely given.
    /// </summary>
    private static List<string> NormalisePurposes(IReadOnlyList<string>? requested)
    {
        var purposes = new List<string> { ConsentPurposes.ServiceCore };

        foreach (var purpose in requested ?? [])
        {
            ValidatePurpose(purpose);

            if (!purposes.Contains(purpose, StringComparer.OrdinalIgnoreCase))
            {
                purposes.Add(purpose);
            }
        }

        return purposes;
    }

    private static void ValidatePurpose(string purpose)
    {
        if (!ConsentPurposes.All.Contains(purpose))
        {
            throw new InvalidOperationException($"Unknown consent purpose '{purpose}'.");
        }
    }

    /// <summary>
    /// Spec 095 §11.3. A self-grant is marked by grantor == subject, and carries
    /// <c>self-authenticated</c>; a parental method on a self-grant, or self-authenticated on a
    /// grant for someone else, are both refused. The two cannot be confused in either direction.
    /// </summary>
    private static void ValidateGrantorAndMethod(GrantConsentRequest request)
    {
        var isSelfGrant = request.GrantedByPartyId == request.SubjectPartyId;
        var isSelfMethod = string.Equals(
            request.VerificationMethod,
            ConsentVerificationMethods.SelfAuthenticated,
            StringComparison.OrdinalIgnoreCase);

        if (!ConsentVerificationMethods.Grantable.Contains(request.VerificationMethod))
        {
            throw new InvalidOperationException(
                $"'{request.VerificationMethod}' is not a valid verification method for a consent grant.");
        }

        if (isSelfMethod && !isSelfGrant)
        {
            throw new InvalidOperationException(
                "self-authenticated cannot be used to consent on another party's behalf.");
        }

        if (!isSelfMethod && isSelfGrant)
        {
            throw new InvalidOperationException(
                "A self-grant must use the self-authenticated method, not a parental one.");
        }
    }

    private static ConsentGrant NewGrant(
        Guid tenantId, Guid subjectPartyId, Guid grantedByPartyId, string purpose,
        string termsVersion, string jurisdiction, string verificationMethod,
        string? verificationRef, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectPartyId = subjectPartyId,
            GrantedByPartyId = grantedByPartyId,
            Purpose = purpose,
            TermsVersion = termsVersion,
            Jurisdiction = jurisdiction,
            VerificationMethod = verificationMethod,
            VerificationRef = verificationRef,
            VerifiedAt = now,
            GrantedAt = now
        };
}
