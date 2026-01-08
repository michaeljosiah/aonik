using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class User : AuditableEntity, ITenantScoped
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string PreferencesJson { get; private set; } = string.Empty;

    private readonly List<UserRole> _userRoles = new();
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    private User() { }

    public User(Guid tenantId, string email, string? phone = null)
    {
        UserId = Id;
        TenantId = tenantId;
        Email = email;
        Phone = phone;
        Status = "Active";
        PreferencesJson = "{}";
    }

    public void UpdateEmail(string email)
    {
        Email = email;
    }

    public void UpdatePhone(string? phone)
    {
        Phone = phone;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void UpdatePreferences(string preferencesJson)
    {
        PreferencesJson = preferencesJson;
    }

    public void AddRole(Role role)
    {
        if (_userRoles.Any(ur => ur.RoleId == role.Id))
            return;

        var userRole = new UserRole(UserId, role.Id);
        _userRoles.Add(userRole);
    }

    public void RemoveRole(Guid roleId)
    {
        var userRole = _userRoles.FirstOrDefault(ur => ur.RoleId == roleId);
        if (userRole != null)
        {
            _userRoles.Remove(userRole);
        }
    }
}
