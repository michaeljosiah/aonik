using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Party.Entities;

public class ExternalAccount : AuditableEntity
{
    public Guid ExternalAccountId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartyId { get; private set; }
    public string ExternalAccountType { get; private set; } = string.Empty;
    public string MaskedIdentifier { get; private set; } = string.Empty;
    public string? ProviderRef { get; private set; }
    public string VerificationStatus { get; private set; } = string.Empty;
    public string MetadataJson { get; private set; } = string.Empty;

    private ExternalAccount() { }

    public ExternalAccount(Guid tenantId, Guid partyId, string externalAccountType, string maskedIdentifier)
    {
        ExternalAccountId = Id;
        TenantId = tenantId;
        PartyId = partyId;
        ExternalAccountType = externalAccountType;
        MaskedIdentifier = maskedIdentifier;
        VerificationStatus = "Pending";
        MetadataJson = "{}";
    }

    public void UpdateProviderRef(string providerRef)
    {
        ProviderRef = providerRef;
    }

    public void UpdateVerificationStatus(string status)
    {
        VerificationStatus = status;
    }

    public void UpdateMetadata(string metadataJson)
    {
        MetadataJson = metadataJson;
    }
}
