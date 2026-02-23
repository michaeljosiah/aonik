namespace Aonik.Platform.Contracts.Models.Settings;

public record SettingResolution(
    string Key,
    string? Value,
    string Source);
