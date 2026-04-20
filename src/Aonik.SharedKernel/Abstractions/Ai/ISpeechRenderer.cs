namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Produces a natural-language speech render of assistant text for TTS.
/// Strips markdown, emojis, and symbols; normalises currency amounts and
/// numbers; and appends chat-review guidance when the turn requires the
/// user to look at or approve something on screen.
/// </summary>
public interface ISpeechRenderer
{
    /// <summary>
    /// Renders <paramref name="assistantText"/> as a TTS-friendly string.
    /// </summary>
    /// <param name="assistantText">The raw assistant response text.</param>
    /// <param name="requiresVisualAttention">
    /// True when the turn invoked a display tool — the speech render will
    /// instruct the user to look at the chat for details.
    /// </param>
    /// <param name="requiresApproval">
    /// True when the turn invoked an approval gate — the speech render will
    /// instruct the user to review and approve in the chat.
    /// </param>
    string Render(string assistantText, bool requiresVisualAttention, bool requiresApproval);

    /// <summary>
    /// Renders a partial chunk of assistant text (e.g. one sentence) for
    /// progressive TTS playback. Produces TTS-friendly text without the
    /// visual-attention / approval guidance suffix.
    /// </summary>
    string RenderChunk(string chunkText);

    /// <summary>
    /// Returns only the visual-attention / approval guidance sentence that
    /// should be appended after sentence-level chunks have all been spoken.
    /// Returns an empty string when no guidance applies.
    /// </summary>
    string RenderGuidance(bool requiresVisualAttention, bool requiresApproval);
}
