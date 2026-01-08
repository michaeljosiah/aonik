using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class PersonalProfile : AuditableEntity, ITenantScoped
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public Guid? HouseholdId { get; set; }
}
