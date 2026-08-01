using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class Household : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    /// <summary>
    /// What kind of group this is — one of <c>GroupKinds</c> (Spec 086 §4). An open string, so a
    /// new shape of group is additive. Existing rows backfill to <c>household</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public List<HouseholdMember> Members { get; set; } = new();
}
