using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class VerificationChallenge : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public VerificationChannel Channel { get; set; }
    public string Target { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public VerificationStatus Status { get; set; }
}
