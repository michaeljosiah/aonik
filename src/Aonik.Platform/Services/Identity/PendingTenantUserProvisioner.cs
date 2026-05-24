using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;

using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Concrete implementation of <see cref="IPendingTenantUserProvisioner"/>.
/// Hardens tenant onboarding: every tenant user (owner or invitee) now
/// gets a placeholder row up front, replacing the previous "create
/// blindly on first login" behavior.
/// </summary>
internal sealed class PendingTenantUserProvisioner : IPendingTenantUserProvisioner
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public PendingTenantUserProvisioner(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }

    public Task<PendingTenantUserResult> ProvisionPendingOwnerAsync(
        Guid tenantId,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
        => ProvisionAsync(
            tenantId,
            email,
            displayName,
            BootstrapIdentityConstants.CreatePendingOwnerSubject(email),
            placeholderKind: "owner",
            existingPartyId: null,
            cancellationToken);

    public Task<PendingTenantUserResult> ProvisionPendingInviteAsync(
        Guid tenantId,
        string email,
        string? displayName,
        Guid? existingPartyId = null,
        CancellationToken cancellationToken = default)
        => ProvisionAsync(
            tenantId,
            email,
            displayName,
            BootstrapIdentityConstants.CreatePendingInviteSubject(email),
            placeholderKind: "invite",
            existingPartyId,
            cancellationToken);

    private async Task<PendingTenantUserResult> ProvisionAsync(
        Guid tenantId,
        string email,
        string? displayName,
        string externalSubject,
        string placeholderKind,
        Guid? existingPartyId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required to provision a pending tenant user.", nameof(email));

        var normalizedEmail = email.Trim();

        // If an existing party was specified, validate it up front so
        // we can't half-create a placeholder for a bogus party id.
        PartyEntity? targetExistingParty = null;
        if (existingPartyId.HasValue && existingPartyId.Value != Guid.Empty)
        {
            targetExistingParty = await _dbContext.Parties
                .FirstOrDefaultAsync(
                    p => p.Id == existingPartyId.Value && p.TenantId == tenantId,
                    cancellationToken);
            if (targetExistingParty == null)
            {
                throw new ArgumentException(
                    $"Party {existingPartyId.Value} was not found in this tenant.",
                    nameof(existingPartyId));
            }
        }

        // Idempotent: if a placeholder for this email already exists in
        // this tenant (under the bootstrap issuer), reuse it. We scan
        // by issuer first so we never collide with a real linked user
        // who happens to share the same email in another tenant.
        var existingPlaceholder = await _dbContext.Users
            .Where(u =>
                u.TenantId == tenantId &&
                u.ExternalIssuer == BootstrapIdentityConstants.PendingOwnerIssuer &&
                u.Email != null)
            .ToListAsync(cancellationToken);

        var existing = existingPlaceholder.FirstOrDefault(u =>
            string.Equals(u.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // Need PartyId + UserPartyId for the result so callers can
            // attach roles or address contacts to the same Party.
            var existingLink = await _dbContext.UserParties
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    up => up.UserId == existing.Id && up.TenantId == tenantId,
                    cancellationToken);

            // If the caller asked to link to a specific existing party,
            // refuse silently changing the linkage on a placeholder that
            // is already attached elsewhere. The idempotent case is when
            // the link target matches.
            if (targetExistingParty != null
                && existingLink != null
                && existingLink.PartyId != targetExistingParty.Id)
            {
                throw new InvalidOperationException(
                    $"User '{normalizedEmail}' is already linked to party {existingLink.PartyId} in this tenant; cannot re-link to {targetExistingParty.Id}.");
            }

            return new PendingTenantUserResult(
                existing.Id,
                existingLink?.PartyId ?? Guid.Empty,
                existingLink?.Id ?? Guid.Empty,
                WasCreated: false);
        }

        var now = _clock.UtcNow;
        var actorId = _currentUserProvider.GetCurrentUserId();
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? normalizedEmail
            : displayName.Trim();

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalIssuer = BootstrapIdentityConstants.PendingOwnerIssuer,
            ExternalSubject = externalSubject,
            Email = normalizedEmail,
            Status = "Active",
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Use the new user's id as the audit actor when no caller is
        // signed in (e.g. during host bootstrap). This matches the
        // previous BootstrapService behavior so audit chains stay
        // continuous after first IdP link.
        var effectiveActorId = actorId ?? newUser.Id;

        // Branch: either link to an existing party (e.g. invite a
        // customer's contact person as a user) or provision a fresh
        // Individual party as before.
        PartyEntity partyToLink;
        PersonProfile? newPersonProfile = null;
        string linkType;
        bool linkedExistingParty;

        if (targetExistingParty != null)
        {
            // Reuse the existing party; do NOT create a new PartyContact
            // (the caller owns the party's contact set already) and do
            // NOT create a PersonProfile (Individual parties already
            // have one; Business parties don't need one for the link).
            partyToLink = targetExistingParty;
            linkType = IsPersonPartyType(targetExistingParty.PartyType)
                ? "Individual"
                : "Employee";
            linkedExistingParty = true;
        }
        else
        {
            // Original behaviour — provision a fresh Individual party
            // for the placeholder, seeded with the invitee's email.
            partyToLink = new PartyEntity
            {
                TenantId = tenantId,
                PartyType = "Individual",
                DisplayName = resolvedDisplayName,
                Status = "Active",
                CreatedAt = now,
                CreatedBy = effectiveActorId,
            };

            partyToLink.Contacts.Add(new PartyContact
            {
                PartyId = partyToLink.Id,
                Type = "Email",
                Value = normalizedEmail,
                IsPrimary = true,
                CreatedAt = now,
                CreatedBy = effectiveActorId,
            });

            _dbContext.Parties.Add(partyToLink);
            await _dbContext.SaveChangesAsync(cancellationToken);

            newPersonProfile = new PersonProfile
            {
                PartyId = partyToLink.Id,
                IdvStatus = "Pending",
                CreatedAt = now,
                CreatedBy = effectiveActorId,
            };
            _dbContext.PersonProfiles.Add(newPersonProfile);
            linkType = "Individual";
            linkedExistingParty = false;
        }

        var userParty = new UserParty
        {
            TenantId = tenantId,
            UserId = newUser.Id,
            PartyId = partyToLink.Id,
            LinkType = linkType,
            CreatedAt = now,
            CreatedBy = effectiveActorId,
        };

        _dbContext.UserParties.Add(userParty);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserProvisioned,
            "User",
            newUser.Id,
            tenantId,
            effectiveActorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                newUser.Id,
                Email = AuditLogMasking.MaskEmail(newUser.Email),
                PartyId = partyToLink.Id,
                UserPartyId = userParty.Id,
                PersonProfileId = newPersonProfile?.Id,
                PlaceholderKind = placeholderKind,
                LinkType = linkType,
                LinkedExistingParty = linkedExistingParty,
                RequiresIdentityLink = true,
            }),
            cancellationToken);

        return new PendingTenantUserResult(
            newUser.Id,
            partyToLink.Id,
            userParty.Id,
            WasCreated: true);
    }

    // Match CustomerAdminService.IsPersonPartyType so the link-type
    // derivation stays consistent across the codebase: "Person" and
    // "Individual" are both treated as human-party labels.
    private static bool IsPersonPartyType(string? partyType)
        => string.Equals(partyType, "Person", StringComparison.OrdinalIgnoreCase)
           || string.Equals(partyType, "Individual", StringComparison.OrdinalIgnoreCase);
}
