using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.ReferenceData.Entities;

public class ReferenceDataItem : AuditableEntity
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? TenantId { get; set; }
}
