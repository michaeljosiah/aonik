namespace Aonik.Platform.Contracts.Api.Settings;

public record SettingValueRequest(string? Value);

public record SettingKeyRequest(string Key);

public record SettingValueUpdateRequest(
    string Key,
    string? Value);

public record SettingValueResponse(
    string Key,
    string? Value,
    string Source);

public record PublicSettingValueResponse(
    string Key,
    string? Value);

public record BatchGetSettingValuesRequest(
    IReadOnlyList<string> Keys);

public record BatchGetSettingValuesResponse(
    IReadOnlyList<SettingValueResponse> Settings);
