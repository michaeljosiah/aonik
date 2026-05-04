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
            cancellationToken);

    public Task<PendingTenantUserResult> ProvisionPendingInviteAsync(
        Guid tenantId,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
        => ProvisionAsync(
            tenantId,
            email,
            displayName,
            BootstrapIdentityConstants.CreatePendingInviteSubject(email),
            placeholderKind: "invite",
            cancellationToken);

    private async Task<PendingTenantUserResult> ProvisionAsync(
        Guid tenantId,
        string email,
        string? displayName,
        string externalSubject,
        string placeholderKind,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required to provision a pending tenant user.", nameof(email));

        var normalizedEmail = email.Trim();

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

        var party = new PartyEntity
        {
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = resolvedDisplayName,
            Status = "Active",
            CreatedAt = now,
            CreatedBy = effectiveActorId,
        };

        party.Contacts.Add(new PartyContact
        {
            PartyId = party.Id,
            Type = "Email",
            Value = normalizedEmail,
            IsPrimary = true,
            CreatedAt = now,
            CreatedBy = effectiveActorId,
        });

        _dbContext.Parties.Add(party);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var userParty = new UserParty
        {
            TenantId = tenantId,
            UserId = newUser.Id,
            PartyId = party.Id,
            LinkType = "Individual",
            CreatedAt = now,
            CreatedBy = effectiveActorId,
        };

        var personProfile = new PersonProfile
        {
            PartyId = party.Id,
            IdvStatus = "Pending",
            CreatedAt = now,
            CreatedBy = effectiveActorId,
        };

        _dbContext.UserParties.Add(userParty);
        _dbContext.PersonProfiles.Add(personProfile);
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
                PartyId = party.Id,
                UserPartyId = userParty.Id,
                PersonProfileId = personProfile.Id,
                PlaceholderKind = placeholderKind,
                RequiresIdentityLink = true,
            }),
            cancellationToken);

        return new PendingTenantUserResult(
            newUser.Id,
            party.Id,
            userParty.Id,
            WasCreated: true);
    }
}
