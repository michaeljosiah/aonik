using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.ExternalAccounts;

/// <summary>
/// Read-only projection of the ExternalAccount entity for cross-module queries.
/// The authoritative ExternalAccount entity lives in Aonik.Platform.
/// TEMPORARY: Will be replaced by service contracts when inter-module
/// communication is fully implemented.
/// </summary>
public class ExternalAccountReadModel : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public string ExternalAccountType { get; set; } = string.Empty;
    public string MaskedIdentifier { get; set; } = string.Empty;
    public string? ProviderRef { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public string? Country { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
