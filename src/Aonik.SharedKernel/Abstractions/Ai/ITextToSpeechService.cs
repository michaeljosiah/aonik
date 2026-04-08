namespace Aonik.SharedKernel.Abstractions.Ai;

public interface ITextToSpeechService
{
    Task<TextToSpeechSynthesisResult> SynthesizeAsync(
        TextToSpeechSynthesisRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TextToSpeechVoiceOption>> GetVoicesAsync(
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<TextToSpeechVoiceCreationResult> CreateVoiceAsync(
        TextToSpeechVoiceCreationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteVoiceAsync(
        string provider,
        string voiceId,
        CancellationToken cancellationToken = default);
}
