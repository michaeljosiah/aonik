using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

/// <summary>
/// A "revoke active sessions" entry per Spec 026 Part 3. The auth
/// middleware checks the most-recent row per user (via FusionCache)
/// on every request; tokens issued before <see cref="RevokedUtc"/>
/// are rejected with HTTP 401. Tokens issued AFTER the revocation
/// are honoured — this is "kill the current sessions," not a ban.
/// Use deactivation for permanent ban semantics.
/// </summary>
public class UserSessionBlocklistEntry : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public DateTime RevokedUtc { get; set; }

    /// <summary>Operator who triggered the revoke. Null when the
    /// revoke was triggered by a system event (e.g. deactivate).</summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>Free-form reason recorded by the operator. Surfaced
    /// in audit logs and the tombstones / sessions UI.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Pruning hint. After this point the row is no longer
    /// useful (tokens older than the longest-lived JWT can no longer
    /// exist) and a maintenance job may delete it.</summary>
    public DateTime ExpiresUtc { get; set; }
}
