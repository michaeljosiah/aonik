using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Platform.Contracts.Models.Settings;

public record TextToSpeechSettingsResponse(
    bool Enabled,
    bool FallbackToNativeOnFailure,
    TextToSpeechVoiceProfileResponse DefaultProfile,
    TextToSpeechPolicyResponse Policy);

public record TextToSpeechVoiceProfileResponse(
    string Provider,
    string VoiceId,
    string? ModelId,
    string? Locale,
    string? OutputFormat,
    Dictionary<string, string?> ProviderOptions);

public record TextToSpeechPolicyResponse(
    int MaxCharactersPerUtterance,
    int MaxRequestsPerMinutePerUser,
    int? MonthlyCharacterBudget);

public record TextToSpeechSettingsUpdate(
    bool Enabled,
    bool FallbackToNativeOnFailure,
    TextToSpeechVoiceProfileUpdate DefaultProfile,
    TextToSpeechPolicyUpdate Policy);

public record TextToSpeechVoiceProfileUpdate(
    string Provider,
    string VoiceId,
    string? ModelId,
    string? Locale,
    string? OutputFormat,
    Dictionary<string, string?>? ProviderOptions);

public record TextToSpeechPolicyUpdate(
    int MaxCharactersPerUtterance,
    int MaxRequestsPerMinutePerUser,
    int? MonthlyCharacterBudget);

public static class TextToSpeechSettingsMappings
{
    public static TextToSpeechSettingsResponse ToResponse(TextToSpeechSettings settings)
    {
        return new TextToSpeechSettingsResponse(
            settings.Enabled,
            settings.FallbackToNativeOnFailure,
            new TextToSpeechVoiceProfileResponse(
                settings.DefaultProfile.Provider,
                settings.DefaultProfile.VoiceId,
                settings.DefaultProfile.ModelId,
                settings.DefaultProfile.Locale,
                settings.DefaultProfile.OutputFormat,
                new Dictionary<string, string?>(settings.DefaultProfile.ProviderOptions, StringComparer.OrdinalIgnoreCase)),
            new TextToSpeechPolicyResponse(
                settings.Policy.MaxCharactersPerUtterance,
                settings.Policy.MaxRequestsPerMinutePerUser,
                settings.Policy.MonthlyCharacterBudget));
    }

    public static TextToSpeechSettings ToSettings(TextToSpeechSettingsUpdate update)
    {
        return new TextToSpeechSettings(
            update.Enabled,
            update.FallbackToNativeOnFailure,
            new TextToSpeechVoiceProfile(
                update.DefaultProfile.Provider,
                update.DefaultProfile.VoiceId,
                update.DefaultProfile.ModelId,
                update.DefaultProfile.Locale,
                update.DefaultProfile.OutputFormat,
                NormalizeOptions(update.DefaultProfile.ProviderOptions)),
            new TextToSpeechPolicy(
                update.Policy.MaxCharactersPerUtterance,
                update.Policy.MaxRequestsPerMinutePerUser,
                update.Policy.MonthlyCharacterBudget));
    }

    private static Dictionary<string, string?> NormalizeOptions(Dictionary<string, string?>? values)
    {
        if (values == null || values.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        return values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => string.IsNullOrWhiteSpace(pair.Value) ? null : pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }
}
