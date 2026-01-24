using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class Party : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string PartyType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CustomerTierCode { get; set; }
    public List<PartyAddress> Addresses { get; set; } = new();
    public List<PartyContact> Contacts { get; set; } = new();
    public List<PartyConsent> Consents { get; set; } = new();
}
