using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class HouseholdMember : AuditableEntity
{
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = string.Empty;
}
