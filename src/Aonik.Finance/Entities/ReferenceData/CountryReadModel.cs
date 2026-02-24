using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.ReferenceData;

/// <summary>
/// Read-only projection of the Country entity for cross-module queries.
/// The authoritative Country entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class CountryReadModel : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string IsoAlpha3 { get; set; } = string.Empty;
    public int? IsoNumeric { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
