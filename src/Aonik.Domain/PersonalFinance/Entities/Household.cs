using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class Household : AuditableEntity, ITenantScoped
{
    public Guid HouseholdId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private readonly List<HouseholdMember> _members = new();
    public IReadOnlyCollection<HouseholdMember> Members => _members.AsReadOnly();

    private Household() { }

    public Household(Guid tenantId, string name)
    {
        HouseholdId = Id;
        TenantId = tenantId;
        Name = name;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void AddMember(HouseholdMember member)
    {
        _members.Add(member);
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member != null)
        {
            _members.Remove(member);
        }
    }
}
