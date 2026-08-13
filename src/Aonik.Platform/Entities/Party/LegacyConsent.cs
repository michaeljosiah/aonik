using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

/// <summary>
/// Archive of pre-Spec-095 <see cref="PartyConsent"/> rows (Spec 095 §13).
///
/// These predate any verification, so they carry none of the fields <see cref="ConsentGrant"/>
/// requires. Putting them there would have meant either failing the migration or abandoning the
/// invariants that make the new record worth having, and sentinel values would put "unknown" inside
/// the field whose entire job is to answer <em>who consented</em>.
///
/// <para>
/// Append-only through the legacy-bundle importer alone — a pre-migration export bundle must still
/// restore, and a strictly read-only archive left it with no valid destination. It authorises
/// nothing: <c>IConsentReader</c> never reads this table.
/// </para>
/// </summary>
public class LegacyConsent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Which export-bundle version this row was restored from, or null if migrated in place.</summary>
    public string? SourceBundleVersion { get; set; }

    /// <summary>The original <c>AnkPartyConsents</c> row id, so a restore is traceable to its source.</summary>
    public Guid? OriginalConsentId { get; set; }
}
