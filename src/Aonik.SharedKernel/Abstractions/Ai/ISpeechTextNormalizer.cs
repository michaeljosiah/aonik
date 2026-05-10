namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Public abstraction over the internal <c>Aonik.Ai.Services.SpeechTextNormalizer</c>.
/// Lives on SharedKernel so non-Ai modules (notably <c>Aonik.Voice</c>) can apply
/// AONIK's canonical "TTS-ready" text transforms — markdown stripping, currency
/// expansion, abbreviation handling, number formatting — without taking a
/// project reference back to <c>Aonik.Ai</c> internals.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 2.
/// </para>
/// </summary>
public interface ISpeechTextNormalizer
{
    /// <summary>
    /// Returns a TTS-friendly form of <paramref name="text"/>: strips markdown,
    /// expands currency symbols and acronyms, normalises numbers and dates.
    /// Returns <see cref="string.Empty"/> for null/whitespace input.
    /// </summary>
    string Normalize(string? text);
}
