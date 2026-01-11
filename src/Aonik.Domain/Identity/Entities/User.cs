using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class User : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    
    // External identity (from IdP)
    public string ExternalIssuer { get; set; } = string.Empty;      // JWT iss
    public string ExternalSubject { get; set; } = string.Empty;     // JWT sub/oid
    public string? ExternalTenantId { get; set; }                   // JWT tid (Entra only)
    
    // User details
    public string? Email { get; set; }                              // Nullable - only if present/verified
    public string? Phone { get; set; }
    public string Status { get; set; } = "Active";                  // Active, Suspended, Deactivated
    public string PreferencesJson { get; set; } = string.Empty;
    
    // Relationships
    public List<UserRole> UserRoles { get; set; } = new();
}
