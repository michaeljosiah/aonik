using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class UserParty : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PartyId { get; set; }
    public string LinkType { get; set; } = string.Empty;
}
