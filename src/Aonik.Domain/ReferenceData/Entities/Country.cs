using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.ReferenceData.Entities;

public class Country : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string IsoAlpha3 { get; set; } = string.Empty;
    public int? IsoNumeric { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
