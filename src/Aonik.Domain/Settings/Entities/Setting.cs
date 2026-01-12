using Aonik.Domain.Settings;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Settings.Entities;

public class Setting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public Aonik.Domain.Settings.SettingScope Scope { get; set; } = Aonik.Domain.Settings.SettingScope.Global;
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}
