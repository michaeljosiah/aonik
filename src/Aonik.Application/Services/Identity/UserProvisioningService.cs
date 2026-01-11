using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Party.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly IAonikDbContext _dbContext;
    private readonly IUserIdentityService _userIdentityService;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UserProvisioningService(
        IAonikDbContext dbContext,
        IUserIdentityService userIdentityService,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _userIdentityService = userIdentityService;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<UserProvisioningResult> EnsureUserAndCustomerAsync(
        IExternalIdentity identity,
        CancellationToken cancellationToken = default)
    {
        if (identity.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required for user provisioning.");
        }

        if (string.IsNullOrWhiteSpace(identity.ExternalIssuer))
        {
            throw new InvalidOperationException("External issuer is required for user provisioning.");
        }

        if (string.IsNullOrWhiteSpace(identity.ExternalSubject))
        {
            throw new InvalidOperationException("External subject is required for user provisioning.");
        }

        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.TenantId == identity.TenantId &&
                u.ExternalIssuer == identity.ExternalIssuer &&
                u.ExternalSubject == identity.ExternalSubject,
                cancellationToken);

        var user = await _userIdentityService.ResolveOrCreateUserAsync(
            identity.ExternalIssuer,
            identity.ExternalSubject,
            identity.ExternalTenantId,
            identity.Email,
            identity.TenantId,
            cancellationToken);

        var userCreated = existingUser == null;
        var existingLink = await _dbContext.UserParties
            .FirstOrDefaultAsync(link =>
                link.TenantId == identity.TenantId &&
                link.UserId == user.Id &&
                link.LinkType == "Individual",
                cancellationToken);

        Party? party = null;
        if (existingLink != null)
        {
            party = await _dbContext.Parties
                .FirstOrDefaultAsync(p => p.TenantId == identity.TenantId && p.PartyId == existingLink.PartyId,
                    cancellationToken);
        }

        var now = _clock.UtcNow;
        var currentUserId = _currentUserProvider.GetCurrentUserId();
        var partyCreated = false;
        var linkCreated = false;

        if (party == null)
        {
            var partyId = existingLink?.PartyId ?? Guid.NewGuid();
            var displayName = !string.IsNullOrWhiteSpace(identity.Email)
                ? identity.Email
                : identity.ExternalSubject;

            party = new Party
            {
                PartyId = partyId,
                TenantId = identity.TenantId,
                PartyType = "Individual",
                DisplayName = displayName,
                Status = "Active",
                CreatedAt = now,
                CreatedBy = currentUserId
            };

            if (!string.IsNullOrWhiteSpace(identity.Email))
            {
                party.Contacts.Add(new PartyContact
                {
                    PartyContactId = Guid.NewGuid(),
                    PartyId = partyId,
                    Type = "Email",
                    Value = identity.Email,
                    IsPrimary = true,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                });
            }

            _dbContext.Parties.Add(party);
            await _dbContext.SaveChangesAsync(cancellationToken);

            partyCreated = true;

            await _auditLogWriter.LogAsync(
                "UserProvisioningPartyCreated",
                "Party",
                party.Id,
                JsonSerializer.Serialize(new
                {
                    party.PartyId,
                    party.DisplayName,
                    user.Id
                }),
                cancellationToken);
        }

        if (existingLink == null)
        {
            var userParty = new UserParty
            {
                TenantId = identity.TenantId,
                UserId = user.Id,
                PartyId = party.PartyId,
                LinkType = "Individual",
                CreatedAt = now,
                CreatedBy = currentUserId
            };

            _dbContext.UserParties.Add(userParty);
            await _dbContext.SaveChangesAsync(cancellationToken);

            linkCreated = true;

            await _auditLogWriter.LogAsync(
                "UserProvisioningPartyLinked",
                "UserParty",
                userParty.Id,
                JsonSerializer.Serialize(new
                {
                    user.Id,
                    party.PartyId,
                    userParty.LinkType
                }),
                cancellationToken);
        }

        return new UserProvisioningResult(
            user.Id,
            party.PartyId,
            userCreated,
            partyCreated,
            linkCreated);
    }
}
