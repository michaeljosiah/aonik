using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyContact : AuditableEntity
{
    public Guid PartyContactId { get; private set; }
    public Guid PartyId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    private PartyContact() { }

    public PartyContact(Guid partyId, string type, string value, bool isPrimary = false)
    {
        PartyContactId = Id;
        PartyId = partyId;
        Type = type;
        Value = value;
        IsPrimary = isPrimary;
    }

    public void UpdateValue(string value)
    {
        Value = value;
    }

    public void SetAsPrimary()
    {
        IsPrimary = true;
    }

    public void UnsetAsPrimary()
    {
        IsPrimary = false;
    }
}
