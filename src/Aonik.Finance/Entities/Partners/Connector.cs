using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

public class Connector : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }

    /// <summary>The connector kind (e.g. <c>flutterwave-payout-v4</c>); maps to a registry entry (Spec 042 §4).</summary>
    public string ConnectorType { get; set; } = string.Empty;

    /// <summary>The immutable <see cref="CredentialBundle.Ref"/> this connector binds to (Spec 042 §6).</summary>
    public string? CredentialsRef { get; set; }

    public string ConfigJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Marks the single connector per kind that the legacy <c>Finance.Partners.Flutterwave.*</c> keys
    /// resolve for during transition (Spec 042 §7.2). At most one default per <c>(TenantId, ConnectorType)</c>,
    /// enforced by a filtered unique index. Any other connector with no bound bundle fails closed rather than
    /// borrowing the global account.
    /// </summary>
    public bool IsLegacyDefault { get; set; }
}
