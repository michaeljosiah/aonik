using System.Text;

namespace Aonik.Agents.Services;

/// <summary>
/// Buffers streaming assistant text and peels off complete sentences as
/// they arrive so they can be sent to TTS progressively. A "complete
/// sentence" is a span ending in <c>.</c>, <c>!</c>, or <c>?</c> (that is
/// not a decimal) followed by whitespace, or a paragraph break.
/// </summary>
internal sealed class SpeechStreamBuffer
{
    private readonly StringBuilder _buffer = new();
    private int _chunkIndex;

    /// <summary>
    /// Index that will be assigned to the next emitted chunk. Starts at 0
    /// and advances after each successful <see cref="TryPopSentence"/> or
    /// <see cref="FlushRemaining"/> call.
    /// </summary>
    public int NextChunkIndex => _chunkIndex;

    public void Append(string text)
    {
        if (!string.IsNullOrEmpty(text))
            _buffer.Append(text);
    }

    /// <summary>
    /// If the buffered text contains a recognisable sentence boundary with
    /// additional content following it, returns the leading completed span
    /// and removes it from the buffer. Only cuts at the FIRST boundary so
    /// TTS can start playback as soon as one sentence is ready.
    /// </summary>
    public bool TryPopSentence(out string rawChunk)
    {
        var text = _buffer.ToString();
        var cutIdx = FindFirstSafeCutIndex(text);

        if (cutIdx < 0)
        {
            rawChunk = string.Empty;
            return false;
        }

        rawChunk = text[..cutIdx].TrimEnd();
        if (rawChunk.Length == 0)
        {
            _buffer.Remove(0, cutIdx);
            return false;
        }

        _buffer.Remove(0, cutIdx);
        _chunkIndex++;
        return true;
    }

    /// <summary>
    /// Returns whatever text remains in the buffer as a final chunk (used
    /// after the upstream stream ends) and clears the buffer. Returns null
    /// when the buffer is empty or contains only whitespace.
    /// </summary>
    public string? FlushRemaining()
    {
        if (_buffer.Length == 0) return null;
        var remaining = _buffer.ToString().Trim();
        _buffer.Clear();
        if (remaining.Length == 0) return null;
        _chunkIndex++;
        return remaining;
    }

    private static int FindFirstSafeCutIndex(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var isTerminator = c is '.' or '!' or '?';
            var isParagraph = c == '\n' && i + 1 < text.Length && text[i + 1] == '\n';

            if (!isTerminator && !isParagraph)
                continue;

            if (isTerminator && IsDecimalSeparator(text, i))
                continue;

            var next = i + 1;

            if (isTerminator)
            {
                if (next >= text.Length) continue;
                if (!char.IsWhiteSpace(text[next])) continue;
            }

            while (next < text.Length && char.IsWhiteSpace(text[next]))
                next++;

            if (next >= text.Length)
                continue;

            return next;
        }

        return -1;
    }

    private static bool IsDecimalSeparator(string text, int index)
    {
        if (text[index] != '.') return false;
        if (index <= 0 || index >= text.Length - 1) return false;
        return char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1]);
    }
}
