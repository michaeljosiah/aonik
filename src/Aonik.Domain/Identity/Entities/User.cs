using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class User : AuditableEntity, ITenantScoped
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PreferencesJson { get; set; } = string.Empty;
    public List<UserRole> UserRoles { get; set; } = new();
}
