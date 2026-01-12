namespace Aonik.Application.Models.Settings;

public record SettingResolution(
    string Key,
    string? Value,
    string Source);
