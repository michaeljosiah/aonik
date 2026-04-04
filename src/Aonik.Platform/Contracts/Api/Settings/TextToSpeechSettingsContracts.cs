namespace Aonik.Platform.Contracts.Api.Settings;

public record GetTextToSpeechVoicesRequest(string? Provider);

public record TextToSpeechVoiceOptionResponse(
    string VoiceId,
    string Name,
    string? PreviewUrl,
    string? Category,
    Dictionary<string, string?> Labels);

public record TextToSpeechPreviewRequest(
    string Text,
    string? Locale,
    string? Provider,
    string? VoiceId,
    string? ModelId,
    string? OutputFormat,
    Dictionary<string, string?>? ProviderOptions);
