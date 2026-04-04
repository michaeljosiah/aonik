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

internal interface ITextToSpeechProvider
{
    string Name { get; }

    Task<TextToSpeechProviderStreamResult> SynthesizeAsync(
        TextToSpeechProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ai.TextToSpeechVoiceOption>> GetVoicesAsync(
        string? apiKey,
        CancellationToken cancellationToken = default);
}
