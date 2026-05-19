using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

/// <summary>
/// Permanent record of a hard-deleted user per Spec 026 Part 2. Created
/// when an operator invokes <c>DELETE /admin/users/{id}</c>. The
/// original <c>AnkUsers</c> row is removed along with the user's PII
/// in <c>AnkAuditLogs</c>, but this tombstone retains the operator's
/// identity, the deletion timestamp, and the operator-supplied reason
/// so compliance can audit deletions. <c>OriginalUserId</c> lets us
/// preserve foreign-key integrity for historical references in tables
/// we don't redact (e.g. ledger entries authored by the deleted user).
/// </summary>
public class UserTombstone : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The Id the user held before deletion. Used for
    /// foreign-key remapping in historical reports.</summary>
    public Guid OriginalUserId { get; set; }

    public DateTime DeletedUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    /// <summary>Operator-supplied reason. Required at delete time
    /// (≥ 10 characters enforced at the endpoint).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Best-effort masked email of the deleted user (for
    /// compliance review of "who was deleted" without storing the
    /// raw PII). Format: <c>j***@example.com</c>.</summary>
    public string? MaskedEmail { get; set; }

    /// <summary>Number of audit-log rows that had PII redacted as part
    /// of the deletion. Surfaced in the tombstones UI.</summary>
    public int AuditRowsRedacted { get; set; }
}
