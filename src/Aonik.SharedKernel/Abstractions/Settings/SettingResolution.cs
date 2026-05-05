namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Resolved settings value with its source. <see cref="Source"/> records
/// which scope ("Global", "Tenant", "User") supplied the value, useful
/// for surfacing override provenance in the admin UI.
/// </summary>
public record SettingResolution(
    string Key,
    string? Value,
    string Source);
