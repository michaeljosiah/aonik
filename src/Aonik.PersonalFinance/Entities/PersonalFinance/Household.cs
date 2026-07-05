using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class Household : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<HouseholdMember> Members { get; set; } = new();
}
