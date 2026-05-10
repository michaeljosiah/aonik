using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Services;

/// <summary>
/// Public DI-injectable facade over the internal <see cref="SpeechTextNormalizer"/>.
/// Voice module + any future caller depend on <see cref="ISpeechTextNormalizer"/>;
/// <c>SpeechTextNormalizer</c> stays internal to <c>Aonik.Ai</c> so its
/// implementation details and Roslyn-generator partial classes don't escape
/// the module.
///
/// <para>
/// Registered in <c>AiModule.ConfigureServices</c> as a singleton — the
/// underlying static method is allocation-free and stateless.
/// </para>
/// </summary>
internal sealed class SpeechTextNormalizerFacade : ISpeechTextNormalizer
{
    public string Normalize(string? text) => SpeechTextNormalizer.Normalize(text);
}
