using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// Typed, queryable capability lane per connector - the persistent twin of the in-memory
/// PartnerConnectorCapability. Authoritative for routing post-migration; Connector.ConfigJson /
/// Partner.CapabilitiesJson are demoted to free-form config and no longer parsed at selection time.
/// Tenant-scoped, following <see cref="Connector"/>.
/// </summary>
public class ConnectorCapability : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ConnectorId { get; set; }

    /// <summary>Payout | Collection | BillPayment | AirtimeTopup.</summary>
    public string Category { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string? Method { get; set; }
    public string Status { get; set; } = string.Empty;
}
