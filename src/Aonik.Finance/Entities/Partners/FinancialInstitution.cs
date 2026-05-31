using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// Global, partner-agnostic directory of banks / MMOs / wallet providers that connector codes map
/// onto (see <see cref="ConnectorInstitutionCode"/>). This is the payout / collection institution
/// directory; airtime telcos are billers (CatalogBiller + ConnectorBillerMapping), not entries here.
///
/// Global with a nullable <see cref="TenantId"/> - a tenant-overridable directory like
/// ReferenceDataItem / Country: null-tenant rows are visible to every tenant. No ITenantScoped, so
/// it is registered via ApplyNullableTenantQueryFilter on the canonical context.
/// </summary>
public class FinancialInstitution : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Bank | MobileMoney | Wallet.</summary>
    public string InstitutionType { get; set; } = string.Empty;

    public string? DefaultCurrency { get; set; }
    public string? Bic { get; set; }
    public bool IsActive { get; set; } = true;
}
