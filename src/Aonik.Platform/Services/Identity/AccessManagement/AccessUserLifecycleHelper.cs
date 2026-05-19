using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Status changes (Activate / Deactivate), the customer-record
/// diagnose / repair flow, plus the Spec 026 revoke + hard-delete
/// flows. The helper is constructed inline per call in
/// <see cref="AccessManagementService"/> so the deps it needs only
/// have to be resolved when the caller actually invokes one of these
/// methods.
/// </summary>
internal sealed class AccessUserLifecycleHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly IUserSessionBlocklist _sessionBlocklist;
    private readonly IIdentityProviderManagementClientFactory _idpClientFactory;
    private readonly ILogger _logger;

    public AccessUserLifecycleHelper(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        IUserSessionBlocklist sessionBlocklist,
        IIdentityProviderManagementClientFactory idpClientFactory,
        ILogger logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _sessionBlocklist = sessionBlocklist;
        _idpClientFactory = idpClientFactory;
        _logger = logger;
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

        // Spec 026 Part 3: auto-revoke any active sessions so the
        // deactivated user is logged out within one cache window.
        // Failure here is logged but doesn't undo the deactivation —
        // the user is already not-Active so the status check in the
        // auth middleware would catch them on the next request anyway.
        try
        {
            await _sessionBlocklist.RevokeAsync(
                tenantId,
                userId,
                revokedByUserId: _currentUserProvider.GetCurrentUserId(),
                reason: "auto-revoke on deactivate",
                cancellationToken);

            await _auditLogWriter.LogAsync(
                AuditEventNames.UserSessionsRevoked,
                "User",
                userId,
                tenantId,
                _currentUserProvider.GetCurrentUserId(),
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new
                {
                    UserId = userId,
                    Reason = "auto-revoke on deactivate",
                    Trigger = "DeactivateUser",
                }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-revoke after deactivate failed for user {UserId} in tenant {TenantId}", userId, tenantId);
        }
    }

    public async Task<RevokeUserSessionsResponse> RevokeSessionsAsync(
        Guid tenantId,
        Guid userId,
        RevokeUserSessionsRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "operator-revoke" : request.Reason.Trim();
        var actorId = _currentUserProvider.GetCurrentUserId();

        var revocation = await _sessionBlocklist.RevokeAsync(
            tenantId,
            userId,
            actorId,
            reason,
            cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserSessionsRevoked,
            "User",
            userId,
            tenantId,
            actorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                UserId = userId,
                Email = AuditLogMasking.MaskEmail(user.Email),
                Reason = reason,
                RevokedUtc = revocation.RevokedUtc,
                ExpiresUtc = revocation.ExpiresUtc,
                Trigger = "ExplicitRevoke",
            }),
            cancellationToken);

        return new RevokeUserSessionsResponse(
            userId,
            revocation.RevokedUtc,
            revocation.ExpiresUtc,
            reason);
    }

    public async Task<DeleteUserResponse> DeleteUserAsync(
        Guid tenantId,
        Guid userId,
        DeleteUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            throw new ArgumentException("A deletion reason of at least 10 characters is required.", nameof(request));
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (!string.Equals((user.Email ?? string.Empty).Trim(), (request.EmailConfirmation ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Email confirmation does not match the user's email address.",
                nameof(request));
        }

        // Defence against locking the tenant out — refuse to delete
        // the last TenantAdmin.
        var isTenantAdmin = await _dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId
                && _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.TenantId == tenantId && r.Name == "TenantAdmin"),
                cancellationToken);
        if (isTenantAdmin)
        {
            var otherTenantAdminCount = await _dbContext.UserRoles
                .AsNoTracking()
                .CountAsync(ur => ur.UserId != userId
                    && _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.TenantId == tenantId && r.Name == "TenantAdmin"),
                    cancellationToken);
            if (otherTenantAdminCount == 0)
            {
                throw new InvalidOperationException(
                    "Cannot delete the last TenantAdmin. Assign the TenantAdmin role to another user first.");
            }
        }

        var actorId = _currentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;
        var maskedEmail = AuditLogMasking.MaskEmail(user.Email);
        var externalIssuer = user.ExternalIssuer;
        var externalSubject = user.ExternalSubject;
        var externalTenantId = user.ExternalTenantId;

        // 1. Auto-revoke active sessions so any in-flight requests
        //    from the deleted user are killed before we drop the row.
        try
        {
            await _sessionBlocklist.RevokeAsync(tenantId, userId, actorId, "pre-delete revoke", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pre-delete session revoke failed for user {UserId}", userId);
        }

        // 2. Call the IdP management API to delete the IdP user. We
        //    swallow errors so an outage doesn't block the platform-
        //    side delete; the failure is reported in the response.
        var idpDeleted = false;
        string? idpFailureReason = null;
        if (!BootstrapIdentityConstants.IsPendingPlaceholderIssuer(externalIssuer)
            && !string.IsNullOrWhiteSpace(externalSubject))
        {
            try
            {
                var client = await _idpClientFactory.GetClientAsync(cancellationToken);
                if (client != null)
                {
                    var result = await client.DeleteUserAsync(externalSubject, externalTenantId, cancellationToken);
                    idpDeleted = result.Deleted;
                    idpFailureReason = result.FailureReason;
                }
                else
                {
                    idpFailureReason = "no_idp_management_client_configured";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IdP delete failed for user {UserId}", userId);
                idpFailureReason = $"exception: {ex.Message}";
            }
        }
        else
        {
            idpFailureReason = "placeholder_user_no_idp_record";
        }

        // 3. Create the tombstone before we drop the row so the
        //    OriginalUserId is captured even if the cascade later
        //    fails.
        var tombstone = new UserTombstone
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalUserId = userId,
            DeletedUtc = now,
            DeletedByUserId = actorId,
            Reason = request.Reason.Trim(),
            MaskedEmail = maskedEmail,
            CreatedAt = now,
            CreatedBy = actorId,
        };
        _dbContext.UserTombstones.Add(tombstone);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 4. Redact PII from audit logs. We only update the
        //    DetailsJson — the action / actor / resource fields are
        //    load-bearing for compliance review, so we replace any
        //    embedded email with the masked form and clear free-form
        //    fields that may have leaked PII.
        var auditRowsRedacted = await RedactAuditLogsAsync(tenantId, userId, user.Email, cancellationToken);
        tombstone.AuditRowsRedacted = auditRowsRedacted;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 5. Cascade-delete the user's roles + invite log rows. Party
        //    + person profile + contacts are intentionally retained:
        //    they belong to the Party (not the User), and other
        //    modules (Ledger, Orders) hold foreign keys into Party.
        //    Wiping the Party would orphan referenced rows; the
        //    cascade-delete here only removes data we own end-to-end.
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);
        if (userRoles.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(userRoles);
        }
        var inviteLogs = await _dbContext.UserInviteLogs
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .ToListAsync(cancellationToken);
        if (inviteLogs.Count > 0)
        {
            _dbContext.UserInviteLogs.RemoveRange(inviteLogs);
        }

        // 6. Drop the user row.
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 7. Final audit-log entry. Uses the tombstone id as
        //    ResourceId since the original user-id is gone.
        await _auditLogWriter.LogAsync(
            AuditEventNames.UserDeleted,
            "User",
            tombstone.Id,
            tenantId,
            actorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                TombstoneId = tombstone.Id,
                OriginalUserId = userId,
                MaskedEmail = maskedEmail,
                Reason = request.Reason.Trim(),
                IdentityProviderUserDeleted = idpDeleted,
                IdentityProviderFailureReason = idpFailureReason,
                AuditRowsRedacted = auditRowsRedacted,
            }),
            cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserAuditRedacted,
            "User",
            tombstone.Id,
            tenantId,
            actorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                TombstoneId = tombstone.Id,
                OriginalUserId = userId,
                AuditRowsRedacted = auditRowsRedacted,
                Reason = "right-to-be-forgotten",
            }),
            cancellationToken);

        return new DeleteUserResponse(
            tombstone.Id,
            userId,
            now,
            auditRowsRedacted,
            idpDeleted);
    }

    /// <summary>
    /// Spec 026 Part 2 redaction: walk every <c>AnkPlatformAuditLogs</c>
    /// row authored by, or referencing, the deleted user and replace
    /// any embedded email + display name in the JSON details payload
    /// with the masked form. The action and resource columns are left
    /// intact so compliance review can still trace what happened.
    /// </summary>
    private async Task<int> RedactAuditLogsAsync(
        Guid tenantId,
        Guid userId,
        string? userEmail,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.AuditLogs
            .Where(a => a.TenantId == tenantId
                && (a.ActorId == userId || a.ResourceId == userId))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        var maskedEmail = AuditLogMasking.MaskEmail(userEmail) ?? "(deleted)";
        var lowered = userEmail?.Trim().ToLowerInvariant();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.DetailsJson))
            {
                continue;
            }

            var redacted = row.DetailsJson;
            if (!string.IsNullOrEmpty(lowered))
            {
                // Case-insensitive replace.
                redacted = System.Text.RegularExpressions.Regex.Replace(
                    redacted,
                    System.Text.RegularExpressions.Regex.Escape(lowered),
                    maskedEmail,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            row.DetailsJson = redacted;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
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

    public async Task<PagedResult<UserTombstoneSummary>> ListTombstonesAsync(
        Guid tenantId,
        ListUsersRequest request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserTombstones
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(t =>
                (t.MaskedEmail != null && t.MaskedEmail.Contains(search)) ||
                t.Reason.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.DeletedUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var operatorIds = items.Where(t => t.DeletedByUserId.HasValue)
            .Select(t => t.DeletedByUserId!.Value)
            .Distinct()
            .ToList();
        var operatorEmails = operatorIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _dbContext.Users
                .AsNoTracking()
                .Where(u => operatorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email })
                .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);

        var summaries = items.Select(t => new UserTombstoneSummary(
            t.Id,
            t.OriginalUserId,
            t.DeletedUtc,
            t.DeletedByUserId,
            t.DeletedByUserId.HasValue && operatorEmails.TryGetValue(t.DeletedByUserId.Value, out var op) ? op : null,
            t.Reason,
            t.MaskedEmail,
            t.AuditRowsRedacted)).ToList();

        return new PagedResult<UserTombstoneSummary>(
            summaries,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }
}
