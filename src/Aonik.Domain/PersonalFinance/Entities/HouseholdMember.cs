using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class HouseholdMember : AuditableEntity
{
    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string PermissionsJson { get; private set; } = string.Empty;

    private HouseholdMember() { }

    public HouseholdMember(Guid householdId, Guid userId, string role)
    {
        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        PermissionsJson = "{}";
    }

    public void UpdateRole(string role)
    {
        Role = role;
    }

    public void UpdatePermissions(string permissionsJson)
    {
        PermissionsJson = permissionsJson;
    }
}
