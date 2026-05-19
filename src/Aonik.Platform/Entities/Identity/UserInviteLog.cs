using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

/// <summary>
/// Per-send audit row for invite emails. Captures every send/resend
/// against an invited user so the admin UI can show "Sent 5 minutes
/// ago" timestamps and so we can enforce per-user send rate limits.
/// Separate from <see cref="Aonik.Platform.Entities.Compliance.AuditLog"/>
/// because audit logs are tamper-evident and append-only; the invite
/// log is cleared when the user record is deleted (cascade).
/// </summary>
public class UserInviteLog : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>"Initial" on the first send; "Resend" on every subsequent send.</summary>
    public string Kind { get; set; } = "Initial";

    public DateTime SentUtc { get; set; }
    public Guid? SentByUserId { get; set; }

    /// <summary>The invite token in effect when this send was issued.
    /// Stored opaquely (truncated prefix) so operators can correlate
    /// without exposing the secret in logs.</summary>
    public string TokenPrefix { get; set; } = string.Empty;

    public DateTime ExpiresUtc { get; set; }
}
