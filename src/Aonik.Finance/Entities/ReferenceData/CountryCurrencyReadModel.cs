using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.ReferenceData;

/// <summary>
/// Read-only projection of the CountryCurrency entity for cross-module queries.
/// The authoritative CountryCurrency entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class CountryCurrencyReadModel : AuditableEntity
{
    public Guid CountryId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
