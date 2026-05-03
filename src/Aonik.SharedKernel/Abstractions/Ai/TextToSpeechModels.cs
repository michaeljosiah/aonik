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

/// <summary>
/// A single window of synthesized audio bytes streamed from a TTS provider.
/// Multiple frames make up the audio for one synthesis call; consumers
/// should concatenate <see cref="Data"/> in arrival order until they see
/// a frame with <see cref="IsFinal"/> set to <c>true</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TtsAiRunId"/> is populated on the first frame of a non-cached
/// synthesis, and on every frame served from cache (where it carries the
/// original synthesis's run id, preserving the audit chain).
/// </para>
/// <para>
/// <see cref="ContentType"/>, <see cref="Provider"/>, and <see cref="VoiceId"/>
/// are repeated on every frame so consumers can route per-frame without
/// holding state across the stream.
/// </para>
/// </remarks>
public record TtsAudioFrame(
    ReadOnlyMemory<byte> Data,
    string ContentType,
    string Provider,
    string VoiceId,
    bool IsFinal,
    bool Cached,
    Guid? TtsAiRunId);

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
