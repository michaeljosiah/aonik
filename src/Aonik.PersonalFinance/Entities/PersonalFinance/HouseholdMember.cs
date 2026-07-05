using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class HouseholdMember : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = string.Empty;
    public string InvitationStatus { get; set; } = string.Empty;
    public Guid? InvitedByUserId { get; set; }
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
