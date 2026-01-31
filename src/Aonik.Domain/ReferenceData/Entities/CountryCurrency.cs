using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.ReferenceData.Entities;

public class CountryCurrency : AuditableEntity
{
    public Guid CountryId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
