using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Handles the user-invite path: validates input, defers to the
/// pending-tenant-user provisioner, attaches roles, mints a one-shot
/// invite token (Spec 026 Part 1), renders the
/// <see cref="NotificationTemplateNames.AdminUserInvitation"/> template,
/// and sends the invite email. Idempotent re-invites reuse the existing
/// placeholder row; a separate "resend" path regenerates the token and
/// re-fires the email under a per-user / 24-hour soft rate limit.
/// </summary>
internal sealed class AccessUserInviteHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly IPendingTenantUserProvisioner _pendingUserProvisioner;
    private readonly INotificationTemplateService _notificationTemplateService;
    private readonly IEmailSender _emailSender;
    private readonly UserLifecycleOptions _lifecycleOptions;
    private readonly ILogger<AccessUserInviteHelper> _logger;

    public AccessUserInviteHelper(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        IPendingTenantUserProvisioner pendingUserProvisioner,
        INotificationTemplateService notificationTemplateService,
        IEmailSender emailSender,
        IOptions<UserLifecycleOptions> lifecycleOptions,
        ILogger<AccessUserInviteHelper> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _pendingUserProvisioner = pendingUserProvisioner;
        _notificationTemplateService = notificationTemplateService;
        _emailSender = emailSender;
        _lifecycleOptions = lifecycleOptions.Value;
        _logger = logger;
    }

    public async Task<InviteUserResponse> InviteUserAsync(
        Guid tenantId,
        InviteUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.", nameof(request));

        var trimmedEmail = request.Email.Trim();
        if (!trimmedEmail.Contains('@') || trimmedEmail.IndexOf('@') == 0 || trimmedEmail.IndexOf('@') == trimmedEmail.Length - 1)
            throw new ArgumentException("Email must be a valid email address.", nameof(request));

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Cannot invite a user without a tenant context.");

        // Validate role IDs early so we don't create a placeholder if
        // the request references a role that doesn't belong to the
        // tenant. This prevents privilege-escalation attempts where
        // an admin in tenant A tries to attach a role from tenant B.
        var requestedRoleIds = request.RoleIds?.Where(id => id != Guid.Empty).Distinct().ToList()
            ?? new List<Guid>();
        if (requestedRoleIds.Count > 0)
        {
            var validRoleIds = await _dbContext.Roles
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId && requestedRoleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var unknown = requestedRoleIds.Except(validRoleIds).ToArray();
            if (unknown.Length > 0)
            {
                throw new ArgumentException(
                    $"One or more roles are not part of this tenant: {string.Join(", ", unknown)}",
                    nameof(request));
            }
        }

        // Create (or reuse) the pending placeholder. The provisioner
        // is idempotent — re-inviting the same email returns the
        // existing row, so we can safely (re-)apply roles below.
        var placeholder = await _pendingUserProvisioner.ProvisionPendingInviteAsync(
            tenantId,
            trimmedEmail,
            request.DisplayName,
            cancellationToken);

        // Refuse to attach invite roles to a user that has ALREADY
        // linked an external identity. That would be a sneaky way to
        // alter another user's role set without going through the
        // proper Users.Manage flow. Updating roles on a real user
        // must go through UpdateUserRolesAsync.
        var placeholderUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == placeholder.UserId, cancellationToken);
        if (placeholderUser == null)
        {
            throw new InvalidOperationException($"Pending placeholder user {placeholder.UserId} was not found after provisioning.");
        }
        if (!BootstrapIdentityConstants.IsPendingPlaceholderIssuer(placeholderUser.ExternalIssuer))
        {
            throw new InvalidOperationException(
                $"User '{trimmedEmail}' is already linked in this tenant; use Users.Manage to update their roles.");
        }

        var assignedRoleIds = new List<Guid>();
        var assignedRoleNames = new List<string>();
        foreach (var roleId in requestedRoleIds)
        {
            var alreadyAssigned = await _dbContext.UserRoles
                .AnyAsync(ur => ur.UserId == placeholder.UserId && ur.RoleId == roleId, cancellationToken);
            if (alreadyAssigned)
            {
                assignedRoleIds.Add(roleId);
                continue;
            }

            _dbContext.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = placeholder.UserId,
                RoleId = roleId,
                CreatedAt = _clock.UtcNow,
            });
            assignedRoleIds.Add(roleId);
        }

        if (assignedRoleIds.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (assignedRoleIds.Count > 0)
        {
            assignedRoleNames = await _dbContext.Roles
                .AsNoTracking()
                .Where(r => assignedRoleIds.Contains(r.Id))
                .OrderBy(r => r.Name)
                .Select(r => r.Name)
                .ToListAsync(cancellationToken);
        }

        var actorId = _currentUserProvider.GetCurrentUserId();

        // Mint a fresh invite token. Stable rule: every InviteUser call
        // rotates the token (the previous one is invalidated). This is
        // safer than re-using a token across sends — if a previous
        // invite email leaked, rotating it on the next invite revokes
        // access from the old recipient.
        var token = GenerateInviteToken();
        var expiresUtc = _clock.UtcNow.AddHours(Math.Max(1, _lifecycleOptions.InviteTokenTtlHours));
        placeholderUser.InviteToken = token;
        placeholderUser.InviteExpiresUtc = expiresUtc;
        placeholderUser.InviteEmailSendCount += 1;
        placeholderUser.InviteEmailSentUtc = _clock.UtcNow;
        // Note: the placeholder's Status column stays "Active" to
        // preserve compatibility with the auth pipeline's
        // "User.Status != Active → reject" guard once the IdP link
        // resolves. The "Invited" lifecycle stage is surfaced through
        // a computed field in AccessUserSummary (when ExternalIssuer
        // is still the bootstrap marker).

        await _dbContext.SaveChangesAsync(cancellationToken);

        bool emailSent = await TrySendInviteEmailAsync(
            tenantId,
            placeholderUser,
            request.DisplayName,
            assignedRoleNames,
            token,
            expiresUtc,
            isResend: false,
            cancellationToken);

        await WriteInviteLogAsync(tenantId, placeholderUser.Id, "Initial", token, expiresUtc, actorId, cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserInvited,
            "User",
            placeholder.UserId,
            tenantId,
            actorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                placeholder.UserId,
                Email = AuditLogMasking.MaskEmail(trimmedEmail),
                placeholder.WasCreated,
                AssignedRoleIds = assignedRoleIds,
                EmailSent = emailSent,
                ExpiresUtc = expiresUtc,
            }),
            cancellationToken);

        if (emailSent)
        {
            await _auditLogWriter.LogAsync(
                AuditEventNames.UserInviteEmailSent,
                "User",
                placeholder.UserId,
                tenantId,
                actorId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new
                {
                    placeholder.UserId,
                    Email = AuditLogMasking.MaskEmail(trimmedEmail),
                    Kind = "Initial",
                    ExpiresUtc = expiresUtc,
                    SendCount = placeholderUser.InviteEmailSendCount,
                }),
                cancellationToken);
        }

        return new InviteUserResponse(
            placeholder.UserId,
            tenantId,
            trimmedEmail,
            request.DisplayName,
            assignedRoleIds,
            EmailSent: emailSent,
            ExpiresUtc: expiresUtc,
            EmailSendCount: placeholderUser.InviteEmailSendCount);
    }

    public async Task<ResendInviteResponse> ResendInviteAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}.");
        }

        if (!BootstrapIdentityConstants.IsPendingPlaceholderIssuer(user.ExternalIssuer))
        {
            throw new InvalidOperationException(
                "User has already accepted their invitation. Resend is only available for unaccepted placeholders.");
        }

        // Rate-limit: count sends in the past 24 hours.
        var since = _clock.UtcNow.AddHours(-24);
        var sendsInWindow = await _dbContext.UserInviteLogs
            .CountAsync(x => x.TenantId == tenantId && x.UserId == userId && x.SentUtc >= since, cancellationToken);

        if (sendsInWindow >= _lifecycleOptions.MaxInviteSendsPer24Hours)
        {
            return new ResendInviteResponse(
                userId,
                user.Email ?? string.Empty,
                EmailSent: false,
                ExpiresUtc: user.InviteExpiresUtc,
                EmailSendCount: user.InviteEmailSendCount,
                RateLimitReason: $"Max {_lifecycleOptions.MaxInviteSendsPer24Hours} sends in 24 hours reached.");
        }

        // Roles assigned at invite time, for the email body.
        var assignedRoleNames = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(_dbContext.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        var token = GenerateInviteToken();
        var expiresUtc = _clock.UtcNow.AddHours(Math.Max(1, _lifecycleOptions.InviteTokenTtlHours));

        user.InviteToken = token;
        user.InviteExpiresUtc = expiresUtc;
        user.InviteEmailSendCount += 1;
        user.InviteEmailSentUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailSent = await TrySendInviteEmailAsync(
            tenantId,
            user,
            displayName: null,
            assignedRoleNames,
            token,
            expiresUtc,
            isResend: true,
            cancellationToken);

        var actorId = _currentUserProvider.GetCurrentUserId();
        await WriteInviteLogAsync(tenantId, user.Id, "Resend", token, expiresUtc, actorId, cancellationToken);

        if (emailSent)
        {
            await _auditLogWriter.LogAsync(
                AuditEventNames.UserInviteEmailSent,
                "User",
                user.Id,
                tenantId,
                actorId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new
                {
                    user.Id,
                    Email = AuditLogMasking.MaskEmail(user.Email),
                    Kind = "Resend",
                    ExpiresUtc = expiresUtc,
                    SendCount = user.InviteEmailSendCount,
                }),
                cancellationToken);
        }

        return new ResendInviteResponse(
            user.Id,
            user.Email ?? string.Empty,
            EmailSent: emailSent,
            ExpiresUtc: expiresUtc,
            EmailSendCount: user.InviteEmailSendCount,
            RateLimitReason: null);
    }

    private async Task WriteInviteLogAsync(
        Guid tenantId,
        Guid userId,
        string kind,
        string token,
        DateTime expiresUtc,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        _dbContext.UserInviteLogs.Add(new UserInviteLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Kind = kind,
            SentUtc = _clock.UtcNow,
            SentByUserId = actorId,
            TokenPrefix = token.Length >= 8 ? token[..8] : token,
            ExpiresUtc = expiresUtc,
            CreatedAt = _clock.UtcNow,
            CreatedBy = actorId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TrySendInviteEmailAsync(
        Guid tenantId,
        User placeholder,
        string? displayName,
        IReadOnlyList<string> assignedRoleNames,
        string token,
        DateTime expiresUtc,
        bool isResend,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(placeholder.Email))
        {
            _logger.LogWarning(
                "Skipping invite email send for placeholder {UserId} — email is empty.",
                placeholder.Id);
            return false;
        }

        try
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            var tenantName = tenant?.Name ?? "Aonik";

            var operatorName = await ResolveOperatorDisplayNameAsync(cancellationToken);

            var rolesSuffix = assignedRoleNames.Count == 0
                ? string.Empty
                : $" with the role{(assignedRoleNames.Count > 1 ? "s" : "")} {string.Join(", ", assignedRoleNames)}";

            var inviteUrl = BuildInviteUrl(tenant?.Subdomain, token);

            var model = new Dictionary<string, object?>
            {
                ["tenant_name"] = tenantName,
                ["tenantName"] = tenantName,
                ["invitee_display_name"] = displayName ?? ExtractEmailLocalPart(placeholder.Email),
                ["inviteeDisplayName"] = displayName ?? ExtractEmailLocalPart(placeholder.Email),
                ["operator_display_name"] = operatorName,
                ["operatorDisplayName"] = operatorName,
                ["roles_granted"] = assignedRoleNames.Count == 0 ? string.Empty : string.Join(", ", assignedRoleNames),
                ["roles_granted_suffix"] = rolesSuffix,
                ["rolesGranted"] = assignedRoleNames.Count == 0 ? string.Empty : string.Join(", ", assignedRoleNames),
                ["invite_url"] = inviteUrl,
                ["inviteUrl"] = inviteUrl,
                ["expiry_utc"] = expiresUtc.ToString("u"),
                ["expiryUtc"] = expiresUtc.ToString("u"),
                ["is_resend"] = isResend,
            };

            var rendered = await _notificationTemplateService.RenderAsync(
                new RenderNotificationTemplateRequest(
                    NotificationTemplateNames.AdminUserInvitation,
                    "Email",
                    model),
                cancellationToken);

            await _emailSender.SendAsync(
                new EmailMessage(placeholder.Email, rendered.Subject, rendered.Body),
                cancellationToken);

            _logger.LogInformation(
                "Sent {Kind} invite email to user {UserId} ({MaskedEmail}); expires {ExpiresUtc}",
                isResend ? "resend" : "initial",
                placeholder.Id,
                AuditLogMasking.MaskEmail(placeholder.Email),
                expiresUtc);

            return true;
        }
        catch (Exception ex)
        {
            // Don't fail the invite creation just because the email
            // didn't go through. The placeholder + token are still
            // valid; the operator can hit "Resend invite" once the
            // template / sender outage is resolved.
            _logger.LogError(
                ex,
                "Failed to send invite email for user {UserId} ({MaskedEmail})",
                placeholder.Id,
                AuditLogMasking.MaskEmail(placeholder.Email));
            return false;
        }
    }

    private async Task<string> ResolveOperatorDisplayNameAsync(CancellationToken cancellationToken)
    {
        var operatorId = _currentUserProvider.GetCurrentUserId();
        if (operatorId == null || operatorId == Guid.Empty)
        {
            return "Your administrator";
        }

        var op = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == operatorId, cancellationToken);
        if (op == null) return "Your administrator";

        return !string.IsNullOrWhiteSpace(op.Email) ? op.Email : "Your administrator";
    }

    private string BuildInviteUrl(string? tenantSubdomain, string token)
    {
        var baseUrl = _lifecycleOptions.AdminUiBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Fall back to a relative path the front-end can resolve.
            return $"/signin?invite={Uri.EscapeDataString(token)}";
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}/signin{separator}invite={Uri.EscapeDataString(token)}";
    }

    private static string ExtractEmailLocalPart(string email)
    {
        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex > 0 ? email[..atIndex] : email;
    }

    /// <summary>
    /// Mints a 32-byte cryptographic-random URL-safe token suitable
    /// for embedding in the invite link. The token is opaque — it is
    /// never used to authenticate on its own; the accept endpoint
    /// also requires a valid IdP bearer token.
    /// </summary>
    internal static string GenerateInviteToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var encoded = Convert.ToBase64String(bytes);
        return encoded
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
