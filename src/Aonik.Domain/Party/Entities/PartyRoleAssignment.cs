using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyRoleAssignment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string ContextType { get; set; } = string.Empty;
    public Guid ContextId { get; set; }
}
