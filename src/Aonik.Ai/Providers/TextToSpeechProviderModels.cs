namespace Aonik.Ai.Providers;

internal sealed record TextToSpeechProviderRequest(
    Guid AiRunId,
    Guid TenantId,
    Guid UserId,
    string Text,
    string? ApiKey,
    string? Locale,
    string VoiceId,
    string? ModelId,
    string? OutputFormat,
    Dictionary<string, string?> ProviderOptions,
    string? PreviousText,
    string? NextText);

internal sealed record TextToSpeechProviderStreamResult(
    Stream AudioStream,
    string ContentType,
    string Provider,
    string VoiceId,
    string? ModelId,
    IDisposable? ResourceToDispose = null);

internal sealed record TextToSpeechCreateVoiceRequest(
    string Name,
    string SampleAudioBase64,
    string? SampleFilename,
    string? ApiKey,
    IReadOnlyList<string>? Languages = null,
    string? Gender = null,
    int? Age = null,
    IReadOnlyList<string>? Tags = null);

internal sealed record TextToSpeechCreateVoiceResult(
    string VoiceId,
    string Name);

internal sealed record TextToSpeechDeleteVoiceRequest(
    string VoiceId,
    string? ApiKey);

internal interface ITextToSpeechProvider
{
    string Name { get; }

    bool SupportsVoiceCreation => false;

    Task<TextToSpeechProviderStreamResult> SynthesizeAsync(
        TextToSpeechProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ai.TextToSpeechVoiceOption>> GetVoicesAsync(
        string? apiKey,
        CancellationToken cancellationToken = default);

    Task<TextToSpeechCreateVoiceResult> CreateVoiceAsync(
        TextToSpeechCreateVoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"Provider '{Name}' does not support voice creation.");
    }

    Task DeleteVoiceAsync(
        TextToSpeechDeleteVoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"Provider '{Name}' does not support voice deletion.");
    }
}
