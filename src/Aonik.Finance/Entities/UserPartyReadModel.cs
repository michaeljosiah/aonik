using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities;

/// <summary>
/// Read-only projection of the UserParty bridge entity for cross-module queries.
/// The authoritative UserParty entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class UserPartyReadModel : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PartyId { get; set; }
}
