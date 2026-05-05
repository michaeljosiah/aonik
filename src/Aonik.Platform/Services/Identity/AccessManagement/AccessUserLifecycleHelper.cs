using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Status changes (Activate / Deactivate) plus the customer-record
/// diagnose / repair flow that ensures a user has the supporting
/// party / role / profile / contact rows downstream UIs expect.
/// </summary>
internal sealed class AccessUserLifecycleHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public AccessUserLifecycleHelper(
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

    public async Task ActivateUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (user.Status == "Active")
        {
            return;
        }

        user.Status = "Active";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = _currentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (user.Status == "Deactivated")
        {
            return;
        }

        user.Status = "Deactivated";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = _currentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDiagnosticResult> DiagnoseUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        var issues = new List<UserDiagnosticIssue>();

        var userParty = await _dbContext.UserParties
            .AsNoTracking()
            .Where(link => link.UserId == userId && link.TenantId == tenantId && link.LinkType == "Individual")
            .OrderByDescending(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (userParty == null)
        {
            issues.Add(new UserDiagnosticIssue(
                "MISSING_PARTY",
                "No party record linked to this user. A party is required for the user to appear as a customer.",
                true));

            return new UserDiagnosticResult(userId, issues.Count > 0, issues);
        }

        var partyId = userParty.PartyId;

        var hasCustomerRole = await _dbContext.PartyRoleAssignments
            .AsNoTracking()
            .AnyAsync(r =>
                r.TenantId == tenantId &&
                r.PartyId == partyId &&
                r.Role == PartyRoles.Customer,
                cancellationToken);

        if (!hasCustomerRole)
        {
            issues.Add(new UserDiagnosticIssue(
                "MISSING_CUSTOMER_ROLE",
                "Party exists but is missing the Customer role assignment. This user will not appear in the Customers list.",
                true));
        }

        var hasPersonProfile = await _dbContext.PersonProfiles
            .AsNoTracking()
            .AnyAsync(p => p.PartyId == partyId, cancellationToken);

        if (!hasPersonProfile)
        {
            issues.Add(new UserDiagnosticIssue(
                "MISSING_PERSON_PROFILE",
                "No person profile found for this party. Profile details, photos, and IDV status will be unavailable.",
                true));
        }

        var hasEmailContact = await _dbContext.PartyContacts
            .AsNoTracking()
            .AnyAsync(c => c.PartyId == partyId && c.Type == "Email", cancellationToken);

        if (!hasEmailContact && !string.IsNullOrWhiteSpace(user.Email))
        {
            issues.Add(new UserDiagnosticIssue(
                "MISSING_EMAIL_CONTACT",
                "User has an email but the party has no email contact record. The email will not appear in customer search results.",
                true));
        }

        return new UserDiagnosticResult(userId, issues.Count > 0, issues);
    }

    public async Task<UserRepairResult> RepairUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var currentUserId = _currentUserProvider.GetCurrentUserId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        var repairs = new List<string>();

        // Ensure party exists
        var userParty = await _dbContext.UserParties
            .Where(link => link.UserId == userId && link.TenantId == tenantId && link.LinkType == "Individual")
            .OrderByDescending(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        Guid partyId;

        if (userParty == null)
        {
            var displayName = !string.IsNullOrWhiteSpace(user.Email)
                ? user.Email
                : $"User {userId:N}";

            var party = new Aonik.Platform.Entities.Party.Party
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartyType = "Individual",
                DisplayName = displayName,
                Status = "Active",
                CreatedAt = now,
                CreatedBy = currentUserId
            };

            _dbContext.Parties.Add(party);

            _dbContext.UserParties.Add(new UserParty
            {
                TenantId = tenantId,
                UserId = userId,
                PartyId = party.Id,
                LinkType = "Individual",
                CreatedAt = now,
                CreatedBy = currentUserId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            partyId = party.Id;
            repairs.Add("Created party record and linked to user");
        }
        else
        {
            partyId = userParty.PartyId;
        }

        // Ensure Customer role
        var hasCustomerRole = await _dbContext.PartyRoleAssignments
            .AnyAsync(r =>
                r.TenantId == tenantId &&
                r.PartyId == partyId &&
                r.Role == PartyRoles.Customer,
                cancellationToken);

        if (!hasCustomerRole)
        {
            _dbContext.PartyRoleAssignments.Add(new PartyRoleAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartyId = partyId,
                Role = PartyRoles.Customer,
                ContextType = "Tenant",
                ContextId = tenantId,
                CreatedAt = now,
                CreatedBy = currentUserId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            repairs.Add("Added Customer role assignment");
        }

        // Ensure person profile
        var hasPersonProfile = await _dbContext.PersonProfiles
            .AnyAsync(p => p.PartyId == partyId, cancellationToken);

        if (!hasPersonProfile)
        {
            _dbContext.PersonProfiles.Add(new Aonik.Platform.Entities.Party.PersonProfile
            {
                PartyId = partyId,
                IdvStatus = "Pending",
                CreatedAt = now,
                CreatedBy = currentUserId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            repairs.Add("Created person profile");
        }

        // Ensure email contact
        var hasEmailContact = await _dbContext.PartyContacts
            .AnyAsync(c => c.PartyId == partyId && c.Type == "Email", cancellationToken);

        if (!hasEmailContact && !string.IsNullOrWhiteSpace(user.Email))
        {
            _dbContext.PartyContacts.Add(new Aonik.Platform.Entities.Party.PartyContact
            {
                PartyId = partyId,
                Type = "Email",
                Value = user.Email,
                IsPrimary = true,
                CreatedAt = now,
                CreatedBy = currentUserId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            repairs.Add("Added email contact to party");
        }

        await _auditLogWriter.LogAsync(
            "UserRepaired",
            "User",
            userId,
            tenantId,
            currentUserId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { UserId = userId, Repairs = repairs }),
            cancellationToken);

        return new UserRepairResult(userId, repairs);
    }
}
