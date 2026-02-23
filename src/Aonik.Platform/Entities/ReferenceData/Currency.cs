using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.ReferenceData;

public class Currency : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NumericCode { get; set; }
    public int? MinorUnit { get; set; }
    public string? WithdrawalDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
