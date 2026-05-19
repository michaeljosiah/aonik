using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Spec 026 Part 1 — anonymous-but-authenticated handler for the
/// <c>POST /identity/invite/accept</c> endpoint. The caller is required
/// to supply (a) a valid IdP bearer token (the endpoint validates it
/// via the standard JWT pipeline so the caller's <c>iss</c>/<c>sub</c>
/// are trusted at this point) and (b) the one-shot invite token.
/// <para>
/// In the normal flow the platform's JWT validator has already
/// matched the email against the placeholder and linked the IdP
/// identity onto it. This service then consumes the token (so it
/// cannot be replayed) and writes an explicit
/// <see cref="AuditEventNames.UserInviteAccepted"/> event so audit
/// review can distinguish "user discovered the platform via the
/// invite email" from "user opened a magic browser tab via JIT".
/// </para>
/// </summary>
internal sealed class InviteAcceptanceService : IInviteAcceptanceService
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly ILogger<InviteAcceptanceService> _logger;

    public InviteAcceptanceService(
        PlatformDbContext dbContext,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        ILogger<InviteAcceptanceService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _logger = logger;
    }

    public async Task<AcceptInviteResponse> AcceptInviteAsync(
        AcceptInviteRequest request,
        string externalIssuer,
        string externalSubject,
        string? externalTenantId,
        string? email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InviteToken))
        {
            return new AcceptInviteResponse(Guid.Empty, Guid.Empty, string.Empty, false, "invite_token_missing");
        }

        // The placeholder row could live in any tenant — the token is
        // unique across the platform. Use AcrossTenants() to bypass the
        // tenant query filter (the invitee may not have tenant context).
        var placeholder = await _dbContext.Users
            .AcrossTenants()
            .FirstOrDefaultAsync(u => u.InviteToken == request.InviteToken, cancellationToken);

        if (placeholder == null)
        {
            // The token might already have been consumed by an earlier
            // call (the JIT linker doesn't consume tokens — only this
            // service does — so the only path to "consumed" is a prior
            // accept). Search for a user that owns this issuer+subject
            // and was recently linked from a placeholder.
            var alreadyLinked = await _dbContext.Users
                .AcrossTenants()
                .FirstOrDefaultAsync(
                    u => u.ExternalIssuer == externalIssuer && u.ExternalSubject == externalSubject,
                    cancellationToken);
            if (alreadyLinked != null && alreadyLinked.InviteAcceptedUtc.HasValue)
            {
                return new AcceptInviteResponse(alreadyLinked.Id, alreadyLinked.TenantId, alreadyLinked.Email ?? string.Empty, true, "already_consumed");
            }

            _logger.LogWarning("Accept invite failed: token not found and no prior accept matches issuer/subject");
            return new AcceptInviteResponse(Guid.Empty, Guid.Empty, string.Empty, false, "invite_not_found");
        }

        if (placeholder.InviteExpiresUtc.HasValue && placeholder.InviteExpiresUtc.Value < _clock.UtcNow)
        {
            _logger.LogWarning("Accept invite failed: token expired for user {UserId}", placeholder.Id);
            return new AcceptInviteResponse(placeholder.Id, placeholder.TenantId, placeholder.Email ?? string.Empty, false, "invite_expired");
        }

        // Two cases:
        //   (a) The placeholder still has the bootstrap issuer — this
        //       is the "we beat the JIT linker" path (rare; the auth
        //       pipeline normally runs first). Link it here.
        //   (b) The placeholder has already been linked by the JIT
        //       linker to a real IdP identity. In this case we only
        //       accept the consume if the linked identity matches the
        //       caller's identity — protects against a stolen invite
        //       token being used against an already-linked placeholder.
        if (BootstrapIdentityConstants.IsPendingPlaceholderIssuer(placeholder.ExternalIssuer))
        {
            // Email must match to prevent token-with-attacker-IdP
            // attack (open question O-1 in the spec — for v1 we
            // require an exact match between the IdP-provided email
            // and the placeholder email).
            if (!string.IsNullOrWhiteSpace(placeholder.Email)
                && !string.IsNullOrWhiteSpace(email)
                && !string.Equals(placeholder.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Accept invite failed: email mismatch (placeholder={Placeholder} vs idp={Idp})",
                    AuditLogMasking.MaskEmail(placeholder.Email),
                    AuditLogMasking.MaskEmail(email));
                return new AcceptInviteResponse(placeholder.Id, placeholder.TenantId, placeholder.Email ?? string.Empty, false, "email_mismatch");
            }

            placeholder.ExternalIssuer = externalIssuer;
            placeholder.ExternalSubject = externalSubject;
            placeholder.ExternalTenantId = externalTenantId;
            if (!string.IsNullOrWhiteSpace(email))
            {
                placeholder.Email = email.Trim();
            }
        }
        else
        {
            // Already-linked case: caller must be the linked identity.
            if (!string.Equals(placeholder.ExternalIssuer, externalIssuer, StringComparison.Ordinal) ||
                !string.Equals(placeholder.ExternalSubject, externalSubject, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Accept invite failed: token issued for user {UserId} but caller identity does not match.",
                    placeholder.Id);
                return new AcceptInviteResponse(placeholder.Id, placeholder.TenantId, placeholder.Email ?? string.Empty, false, "identity_mismatch");
            }
        }

        // Consume the token unconditionally now: any future request
        // with the same token returns invite_not_found.
        placeholder.InviteAcceptedUtc = _clock.UtcNow;
        placeholder.InviteToken = null;
        placeholder.InviteExpiresUtc = null;
        placeholder.LastLoginAt = _clock.UtcNow;
        placeholder.Status = "Active";

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserInviteAccepted,
            "User",
            placeholder.Id,
            placeholder.TenantId,
            placeholder.Id,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                placeholder.Id,
                Email = AuditLogMasking.MaskEmail(placeholder.Email),
                ExternalIssuer = externalIssuer,
                ExternalSubject = externalSubject,
            }),
            cancellationToken);

        return new AcceptInviteResponse(
            placeholder.Id,
            placeholder.TenantId,
            placeholder.Email ?? string.Empty,
            Accepted: true,
            FailureReason: null);
    }
}
