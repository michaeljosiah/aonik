using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities;

/// <summary>
/// Read-only projection of the Party entity for cross-module queries.
/// The authoritative Party entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class PartyReadModel : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CustomerTierCode { get; set; }
}
