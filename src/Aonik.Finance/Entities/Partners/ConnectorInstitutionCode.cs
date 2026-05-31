using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// Maps one <see cref="FinancialInstitution"/> to a connector-specific code (e.g. Flutterwave
/// <c>044</c>, an eTranzact bank code). Many per institution - one per connector. Tenant-scoped,
/// following <see cref="Connector"/>.
/// </summary>
public class ConnectorInstitutionCode : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ConnectorId { get; set; }
    public Guid FinancialInstitutionId { get; set; }
    public string ProviderInstitutionCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
