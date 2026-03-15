using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class HouseholdMember : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = string.Empty;
}
