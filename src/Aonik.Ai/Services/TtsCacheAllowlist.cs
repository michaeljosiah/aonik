using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;

namespace Aonik.Ai.Services;

/// <summary>
/// The static set of agent-emitted phrases that are eligible for TTS
/// caching. Lives in code rather than configuration so the surface is
/// reviewable and version-controlled — adding a new phrase requires a
/// PR.
/// </summary>
/// <remarks>
/// <para>
/// In v1 the list is intentionally narrow: the deterministic guidance
/// strings emitted by <c>SpeechRenderer</c> plus a handful of common
/// agent acknowledgements. User-generated content stays out of cache.
/// </para>
/// <para>
/// Matching is exact and case-sensitive against the post-normalizer
/// text. If a phrase ever needs a personalisation hole (e.g. a name)
/// it does not belong on this list.
/// </para>
/// </remarks>
internal static class TtsCacheAllowlist
{
    private static readonly FrozenSet<string> Phrases = new[]
    {
        "I've opened the chat so you can review.",
        "I've opened the chat so you can review and confirm.",
        "Sorry, I had trouble understanding.",
        "Sure, let me check that for you.",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool Contains(string normalizedText) =>
        !string.IsNullOrEmpty(normalizedText) && Phrases.Contains(normalizedText);

    public static string HashText(string normalizedText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));
}
