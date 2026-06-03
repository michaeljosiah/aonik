using System.Text;

namespace Aonik.Documents.Services;

/// <summary>
/// Splits extracted document text into overlapping, word-bounded chunks for embedding
/// (Spec 035 §13). Lifted unchanged in behaviour from the legacy <c>DocumentUploadEndpoint</c>
/// (512 words/chunk, 100-word overlap) so the unified pipeline embeds documents identically to the
/// path it replaces. Pure and deterministic — no I/O, no dependencies.
/// </summary>
internal static class TextChunker
{
    public const int DefaultChunkSizeWords = 512;
    public const int DefaultOverlapWords = 100;

    private static readonly char[] WordSeparators = { ' ', '\n', '\r', '\t' };

    /// <summary>
    /// Chunks <paramref name="text"/> into pieces of at most <paramref name="chunkSizeWords"/>
    /// words, each sharing <paramref name="overlapWords"/> trailing words with the next, so
    /// context that straddles a boundary is retrievable from either side. Returns an empty list
    /// for blank input.
    /// </summary>
    public static IReadOnlyList<string> Chunk(
        string text,
        int chunkSizeWords = DefaultChunkSizeWords,
        int overlapWords = DefaultOverlapWords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        if (chunkSizeWords < 1)
        {
            chunkSizeWords = DefaultChunkSizeWords;
        }

        // Overlap must be strictly smaller than the chunk size, otherwise the carried-over words
        // alone meet the threshold and the loop never advances.
        if (overlapWords < 0 || overlapWords >= chunkSizeWords)
        {
            overlapWords = Math.Min(DefaultOverlapWords, chunkSizeWords - 1);
        }

        var words = text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        var currentChunk = new StringBuilder();
        var currentWordCount = 0;

        foreach (var word in words)
        {
            currentChunk.Append(word).Append(' ');
            currentWordCount++;

            if (currentWordCount >= chunkSizeWords)
            {
                chunks.Add(currentChunk.ToString().Trim());

                // Seed the next chunk with the last N words so context overlaps the boundary.
                var carried = currentChunk.ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .TakeLast(overlapWords)
                    .ToList();

                currentChunk.Clear();
                if (carried.Count > 0)
                {
                    currentChunk.AppendJoin(' ', carried).Append(' ');
                }

                currentWordCount = carried.Count;
            }
        }

        if (currentChunk.Length > 0)
        {
            var tail = currentChunk.ToString().Trim();
            if (tail.Length > 0)
            {
                chunks.Add(tail);
            }
        }

        return chunks;
    }
}
