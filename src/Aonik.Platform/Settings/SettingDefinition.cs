namespace Aonik.Platform.Settings;

public record SettingDefinition(
    string Key,
    string? DefaultValue = null,
    bool IsEncrypted = false,
    bool IsVisibleToClients = false);
