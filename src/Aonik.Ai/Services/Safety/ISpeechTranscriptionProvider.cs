using Aonik.SharedKernel.Abstractions.Safety;

namespace Aonik.Ai.Services.Safety;

/// <summary>Provider transport only; model routing, consent and run recording remain in the AI module.</summary>
public interface ISpeechTranscriptionProvider : ITemporalCoverage
{
    string Provider { get; }
    Task<string> TranscribeAsync(string reference, string modelName, CancellationToken cancellationToken = default);
}
