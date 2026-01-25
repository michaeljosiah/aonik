using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyRelationship : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid FromPartyId { get; set; }
    public Guid ToPartyId { get; set; }
    public string RelationshipTypeCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
