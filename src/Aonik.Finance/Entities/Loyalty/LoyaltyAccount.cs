using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Loyalty;

/// <summary>Owner serialization and display state. Balances are read from journal legs.</summary>
public sealed class LoyaltyAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public Guid LedgerId { get; set; }
    public Guid LiabilityAccountId { get; set; }
    public long HighestFivePoundMarkSeen { get; set; }
}
