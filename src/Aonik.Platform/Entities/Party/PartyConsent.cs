using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class PartyConsent : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
