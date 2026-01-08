using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyConsent : AuditableEntity
{
    public Guid PartyConsentId { get; private set; }
    public Guid PartyId { get; private set; }
    public string ConsentType { get; private set; } = string.Empty;
    public DateTime GrantedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private PartyConsent() { }

    public PartyConsent(Guid partyId, string consentType)
    {
        PartyConsentId = Id;
        PartyId = partyId;
        ConsentType = consentType;
        GrantedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }

    public bool IsActive()
    {
        return !RevokedAt.HasValue;
    }
}
