using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Settings;

public class Setting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public SettingScope Scope { get; set; } = SettingScope.Global;
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}
