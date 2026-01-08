using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class PersonalProfile : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartyId { get; private set; }
    public Guid? HouseholdId { get; private set; }

    private PersonalProfile() { }

    public PersonalProfile(Guid userId, Guid tenantId, Guid partyId, Guid? householdId = null)
    {
        UserId = userId;
        TenantId = tenantId;
        PartyId = partyId;
        HouseholdId = householdId;
    }

    public void JoinHousehold(Guid householdId)
    {
        HouseholdId = householdId;
    }

    public void LeaveHousehold()
    {
        HouseholdId = null;
    }
}
