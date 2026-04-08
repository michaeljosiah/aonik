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

public record CreateTextToSpeechVoiceRequest(
    string Provider,
    string Name,
    string SampleAudioBase64,
    string? SampleFilename = null,
    List<string>? Languages = null,
    string? Gender = null,
    int? Age = null,
    List<string>? Tags = null);

public record CreateTextToSpeechVoiceResponse(
    string VoiceId,
    string Name,
    string Provider);

public record DeleteTextToSpeechVoiceRequest(
    string Provider,
    string VoiceId);
