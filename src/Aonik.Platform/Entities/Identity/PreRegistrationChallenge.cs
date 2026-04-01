using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class PreRegistrationChallenge : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public VerificationStatus Status { get; set; }
}
