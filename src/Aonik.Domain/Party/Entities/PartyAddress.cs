using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class PartyAddress : AuditableEntity
{
    public Guid PartyAddressId { get; private set; }
    public Guid PartyId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string? Line3 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? State { get; private set; }
    public string Postcode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;

    private PartyAddress() { }

    public PartyAddress(Guid partyId, string type, string line1, string city, string postcode, string country)
    {
        PartyAddressId = Id;
        PartyId = partyId;
        Type = type;
        Line1 = line1;
        City = city;
        Postcode = postcode;
        Country = country;
    }

    public void UpdateAddress(string line1, string? line2, string? line3, string city, string? state, string postcode, string country)
    {
        Line1 = line1;
        Line2 = line2;
        Line3 = line3;
        City = city;
        State = state;
        Postcode = postcode;
        Country = country;
    }
}
