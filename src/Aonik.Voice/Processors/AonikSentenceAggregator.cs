using System.Text;
using Voxa.Frames;
using Voxa.Processors;

namespace Aonik.Voice.Processors;

/// <summary>
/// AONIK-flavoured drop-in replacement for <c>Voxa.Speech.SentenceAggregator</c>. Buffers
/// streaming <see cref="LlmTextChunkFrame"/>s into whole-sentence <see cref="TextFrame"/>s
/// for the downstream <c>TextToSpeechProcessor</c>.
///
/// <para>
/// <b>The fix vs upstream.</b> Voxa's aggregator treats a sentence-ending punctuation
/// mark as a boundary when it's followed by whitespace <i>or by end-of-buffer</i>. The
/// "end-of-buffer" branch is the bug — when an LLM streams <c>"It costs $10,000."</c>
/// as one chunk and <c>"00 plus tax."</c> as the next, the first chunk's trailing
/// <c>.</c> sits at end-of-buffer so the aggregator flushes <c>"It costs $10,000."</c>
/// as a "complete" sentence; the next chunk's <c>"00 plus tax."</c> then becomes a
/// brand-new utterance ("zero zero plus tax").
/// </para>
///
/// <para>
/// This implementation requires the boundary to be followed by whitespace
/// <b>in-stream</b> — end-of-buffer is no longer enough. Leftover content gets flushed
/// when <see cref="LlmTurnEndedFrame"/> fires (the agent loop emits it at end-of-turn)
/// or <see cref="EndFrame"/> arrives (session shutdown), so the final sentence of a
/// response still reaches TTS even when it doesn't end in whitespace.
/// </para>
///
/// <para>
/// Abbreviations like <c>"Mr."</c> aren't handled — that needs a curated dictionary
/// which we don't ship yet. The upstream Voxa aggregator has the same gap.
/// </para>
/// </summary>
public sealed class AonikSentenceAggregator : FrameProcessor
{
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();

    /// <summary>
    /// Maximum chars to buffer before forcing a flush even without a sentence boundary.
    /// Same default as upstream Voxa — guards against a runaway LLM response stalling TTS.
    /// </summary>
    public int MaxBufferChars { get; init; } = 500;

    public AonikSentenceAggregator() : base("AonikSentenceAggregator") { }

    protected override async ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
    {
        switch (frame)
        {
            case LlmTextChunkFrame chunk when !string.IsNullOrEmpty(chunk.Text):
                await OnChunkAsync(chunk.Text, ct).ConfigureAwait(false);
                return;

            case LlmTurnEndedFrame:
                // The agent loop fires this once per turn. Flush whatever's left so the
                // last sentence of the response reaches TTS without waiting for session
                // shutdown. Forward the frame downstream too — other processors may rely
                // on it.
                await FlushLeftoverAsync(ct).ConfigureAwait(false);
                await PushFrameAsync(frame, ct).ConfigureAwait(false);
                return;

            case UserStartedSpeakingFrame:
            case InterruptionFrame:
                // Drop the buffered partial — the assistant turn is being abandoned.
                lock (_lock) _buffer.Clear();
                await PushFrameAsync(frame, ct).ConfigureAwait(false);
                return;

            default:
                await PushFrameAsync(frame, ct).ConfigureAwait(false);
                return;
        }
    }

    protected override async ValueTask OnEndAsync(EndFrame frame, CancellationToken ct)
    {
        await FlushLeftoverAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask OnChunkAsync(string text, CancellationToken ct)
    {
        string? toFlush = null;
        lock (_lock)
        {
            _buffer.Append(text);

            var content = _buffer.ToString();
            var lastBoundary = FindLastSentenceBoundary(content);

            if (lastBoundary >= 0)
            {
                toFlush = content[..(lastBoundary + 1)].Trim();
                _buffer.Clear();
                _buffer.Append(content[(lastBoundary + 1)..]);
            }
            else if (_buffer.Length >= MaxBufferChars)
            {
                // Hard cap — emit whatever we have so TTS doesn't stall on a runaway response.
                toFlush = _buffer.ToString().Trim();
                _buffer.Clear();
            }
        }

        if (!string.IsNullOrEmpty(toFlush))
        {
            await PushFrameAsync(new TextFrame(toFlush), ct).ConfigureAwait(false);
        }
    }

    private async ValueTask FlushLeftoverAsync(CancellationToken ct)
    {
        string? leftover;
        lock (_lock)
        {
            leftover = _buffer.Length > 0 ? _buffer.ToString().Trim() : null;
            _buffer.Clear();
        }
        if (!string.IsNullOrEmpty(leftover))
        {
            await PushFrameAsync(new TextFrame(leftover), ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the index of the last char in <paramref name="s"/> that ends a sentence,
    /// or -1 if none qualify. A char qualifies when it's <c>.</c>, <c>!</c>, <c>?</c>,
    /// or <c>\n</c> AND is followed by whitespace in the buffer. End-of-buffer is
    /// intentionally NOT a boundary — the last sentence of a response is flushed by
    /// <see cref="FlushLeftoverAsync"/> when the turn / session ends. Within a single
    /// in-flight chunk this rule also keeps <c>3.14</c>, <c>v1.5</c>, and
    /// <c>$10,000.00</c> intact because the dot between digits is never followed by
    /// whitespace.
    /// </summary>
    internal static int FindLastSentenceBoundary(string s)
    {
        for (var i = s.Length - 1; i >= 0; i--)
        {
            var c = s[i];
            if (c != '.' && c != '!' && c != '?' && c != '\n')
            {
                continue;
            }
            if (i == s.Length - 1) continue;
            if (!char.IsWhiteSpace(s[i + 1])) continue;
            return i;
        }
        return -1;
    }
}
