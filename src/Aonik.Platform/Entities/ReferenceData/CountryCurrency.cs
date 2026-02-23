using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.ReferenceData;

public class CountryCurrency : AuditableEntity
{
    public Guid CountryId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
