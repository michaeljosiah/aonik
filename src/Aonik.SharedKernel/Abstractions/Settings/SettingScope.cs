namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Resolution scope for a settings lookup.
/// <list type="bullet">
///   <item><b>Global</b>: platform-wide value, no tenant or user.</item>
///   <item><b>Tenant</b>: per-tenant override.</item>
///   <item><b>User</b>: per-user override.</item>
/// </list>
/// </summary>
public enum SettingScope
{
    Global,
    Tenant,
    User
}
