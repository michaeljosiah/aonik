using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class MarketingPreference : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool News { get; set; } = true;
    public bool Offers { get; set; } = true;
    public bool Surveys { get; set; }
    public Party Party { get; set; } = null!;
}
