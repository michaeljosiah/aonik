using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class CategorisationRule : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
