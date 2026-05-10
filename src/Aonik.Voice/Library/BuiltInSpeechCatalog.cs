using Aonik.SharedKernel.Abstractions.Ai.Speech;

namespace Aonik.Voice.Library;

/// <summary>
/// Intentionally empty built-in catalog. Earlier revisions shipped a hard-coded set of
/// archetype providers and recipes that tenants would clone into editable rows; that pattern
/// was removed when we adopted the "create-your-own" flow (admin starts with an empty list,
/// hits "Add provider" / "New recipe" to build their own from scratch).
///
/// <para>
/// The interface is kept so the resolver paths in <c>SpeechProviderLibraryService</c> and
/// <c>VoiceRecipeLibraryService</c> still compile and short-circuit cleanly when asked to
/// resolve a <c>built-in:*</c> id. The reserved prefix in <see cref="SpeechLibraryConstants"/>
/// also stays — old recipe rows from before this change may still carry built-in references,
/// in which case the resolver returns null and the UI surfaces a "missing provider" hint.
/// </para>
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> for the
/// architectural rationale.
/// </para>
/// </summary>
internal sealed class BuiltInSpeechCatalog : IBuiltInSpeechCatalog
{
    public IReadOnlyList<SpeechProvider> AllProviders { get; } = Array.Empty<SpeechProvider>();

    public SpeechProvider? FindProvider(string builtInId) => null;

    public IReadOnlyList<VoiceRecipe> AllRecipes { get; } = Array.Empty<VoiceRecipe>();

    public VoiceRecipe? FindRecipe(string builtInId) => null;
}
