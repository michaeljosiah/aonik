namespace Aonik.SharedKernel.Abstractions.Ai;

public record TextToSpeechVoiceProfile(
    string Provider,
    string VoiceId,
    string? ModelId,
    string? Locale,
    string? OutputFormat,
    Dictionary<string, string?> ProviderOptions);

public record TextToSpeechPolicy(
    int MaxCharactersPerUtterance,
    int MaxRequestsPerMinutePerUser,
    int? MonthlyCharacterBudget);

public record TextToSpeechSettings(
    bool Enabled,
    bool FallbackToNativeOnFailure,
    TextToSpeechVoiceProfile DefaultProfile,
    TextToSpeechPolicy Policy);

public record TextToSpeechVoiceOption(
    string VoiceId,
    string Name,
    string? PreviewUrl,
    string? Category,
    Dictionary<string, string?> Labels);

public record TextToSpeechSynthesisRequest(
    string SpeechText,
    string? Locale,
    string? ThreadId,
    string? MessageId,
    string? UseCase = null,
    TextToSpeechVoiceProfile? VoiceProfileOverride = null);

public record TextToSpeechSynthesisResult(
    Stream AudioStream,
    string ContentType,
    string Provider,
    string VoiceId,
    Guid AiRunId,
    IDisposable? ResourceToDispose = null);

public record TextToSpeechVoiceCreationRequest(
    string Provider,
    string Name,
    string SampleAudioBase64,
    string? SampleFilename = null,
    IReadOnlyList<string>? Languages = null,
    string? Gender = null,
    int? Age = null,
    IReadOnlyList<string>? Tags = null);

public record TextToSpeechVoiceCreationResult(
    string VoiceId,
    string Name,
    string Provider);
