using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class Party : AuditableEntity
{
    public Guid PartyId { get; private set; }
    public Guid TenantId { get; private set; }
    public string PartyType { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;

    private readonly List<PartyAddress> _addresses = new();
    public IReadOnlyCollection<PartyAddress> Addresses => _addresses.AsReadOnly();

    private readonly List<PartyContact> _contacts = new();
    public IReadOnlyCollection<PartyContact> Contacts => _contacts.AsReadOnly();

    private readonly List<PartyConsent> _consents = new();
    public IReadOnlyCollection<PartyConsent> Consents => _consents.AsReadOnly();

    private Party() { }

    public Party(Guid tenantId, string partyType, string displayName)
    {
        PartyId = Id;
        TenantId = tenantId;
        PartyType = partyType;
        DisplayName = displayName;
        Status = "Active";
    }

    public void UpdateDisplayName(string displayName)
    {
        DisplayName = displayName;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void AddAddress(PartyAddress address)
    {
        _addresses.Add(address);
    }

    public void RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (address != null)
        {
            _addresses.Remove(address);
        }
    }

    public void AddContact(PartyContact contact)
    {
        _contacts.Add(contact);
    }

    public void RemoveContact(Guid contactId)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact != null)
        {
            _contacts.Remove(contact);
        }
    }

    public void AddConsent(PartyConsent consent)
    {
        _consents.Add(consent);
    }
}
