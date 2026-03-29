using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class PartyAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string MaskedIdentifier { get; set; } = string.Empty;
    public string? ProviderRef { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public string? Country { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
