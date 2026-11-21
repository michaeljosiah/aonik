using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Integrations;

public sealed class ConnectorConnection : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ConnectorKey { get; set; } = string.Empty;
    public string Ownership { get; set; } = "User";
    public Guid? OwnerUserId { get; set; }
    public Guid ProvisionedByUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;
    public string ProviderAccountLabel { get; set; } = string.Empty;
    public string GrantedScopesJson { get; set; } = "[]";
    public string ProtectedCredentials { get; set; } = string.Empty;
    public int CredentialVersion { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string Status { get; set; } = "PendingConsent";
    public Guid? RefreshLeaseId { get; set; }
    public DateTime? RefreshLeaseExpiresAt { get; set; }
    public DateTime? LastHealthyAt { get; set; }
    public string? LastErrorCode { get; set; }
}
