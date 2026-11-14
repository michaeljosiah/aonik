using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Integrations;

public sealed class ConnectorConnectionRequest : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RequesterUserId { get; set; }
    public string ConnectorKey { get; set; } = string.Empty;
    public string? DesiredCapability { get; set; }
    public string Ownership { get; set; } = "User";
    public Guid? OwnerUserId { get; set; }
    public string ActionsJson { get; set; } = "[]";
    public string RequiredScopesJson { get; set; } = "[]";
    public string Purpose { get; set; } = string.Empty;
    public Guid? WorkflowId { get; set; }
    public Guid? RunId { get; set; }
    public Guid? AutomationIdentityId { get; set; }
    public Guid? AutomationGrantId { get; set; }
    public Guid? AiRunId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string? ActiveRequestHash { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string ReasonCode { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public Guid? ReviewerRoleId { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid? ConnectionId { get; set; }
    public Guid? GrantId { get; set; }
    public Guid? WorkflowGrantId { get; set; }
    public Guid? ReviewTaskId { get; set; }
    public string? OAuthStateHash { get; set; }
    public string? ProtectedVerifier { get; set; }
    public Guid? OAuthStartedByUserId { get; set; }
    public DateTime? OAuthExpiresAt { get; set; }
    public Guid? OAuthLeaseId { get; set; }
    public DateTime? OAuthLeaseExpiresAt { get; set; }
    public string? ProtectedOAuthResult { get; set; }
    public string? ConsentedAccountId { get; set; }
    public string? ConsentedAccountLabel { get; set; }
    public DateTime? ConsentedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public Guid? FulfilledByUserId { get; set; }
}
