using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities;

/// <summary>
/// Read-only projection of the PartyRelationship entity for cross-module queries.
/// The authoritative PartyRelationship entity lives in Aonik.Platform.
/// </summary>
public class PartyRelationshipReadModel : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid FromPartyId { get; set; }
    public Guid ToPartyId { get; set; }
    public string RelationshipTypeCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}
