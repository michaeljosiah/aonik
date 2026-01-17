using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyConsent : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
