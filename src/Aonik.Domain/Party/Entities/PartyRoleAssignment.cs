using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyRoleAssignment : AuditableEntity
{
    public Guid PartyRoleAssignmentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartyId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string ContextType { get; private set; } = string.Empty;
    public Guid ContextId { get; private set; }

    private PartyRoleAssignment() { }

    public PartyRoleAssignment(Guid tenantId, Guid partyId, string role, string contextType, Guid contextId)
    {
        PartyRoleAssignmentId = Id;
        TenantId = tenantId;
        PartyId = partyId;
        Role = role;
        ContextType = contextType;
        ContextId = contextId;
    }

    public void UpdateRole(string role)
    {
        Role = role;
    }
}
