namespace Aonik.Application.Tests.Documents;

using System;
using System.Linq;
using Aonik.Documents.Services;
using FluentAssertions;
using Xunit;

/// <summary>
/// The chunker is the deterministic seam of the ingestion pipeline (Spec 035 §13). These cover the
/// boundaries that matter for retrieval: blank input, fit-in-one-chunk, multi-chunk overlap, and the
/// non-advancing-overlap guard (an overlap ≥ chunk size would otherwise loop forever).
/// </summary>
public sealed class TextChunkerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void Chunk_Should_Return_Empty_For_Blank_Input(string? text)
        => TextChunker.Chunk(text!).Should().BeEmpty();

    [Fact]
    public void Chunk_Should_Return_Single_Chunk_When_Text_Fits()
    {
        var result = TextChunker.Chunk("one two three four five", chunkSizeWords: 10, overlapWords: 2);

        result.Should().ContainSingle();
        result[0].Should().Be("one two three four five");
    }

    [Fact]
    public void Chunk_Should_Split_Into_Multiple_Overlapping_Chunks()
    {
        var words = Enumerable.Range(1, 12).Select(i => $"w{i}").ToArray();
        var text = string.Join(' ', words);

        var result = TextChunker.Chunk(text, chunkSizeWords: 5, overlapWords: 2);

        result.Count.Should().BeGreaterThan(1);
        result.Should().OnlyContain(c => c.Split(' ').Length <= 5, "no chunk exceeds the word budget");

        // The last 2 words of a chunk seed the next, so boundary-straddling context is retrievable.
        var firstChunkWords = result[0].Split(' ');
        var secondChunkWords = result[1].Split(' ');
        secondChunkWords.Take(2).Should().Equal(firstChunkWords.TakeLast(2));
    }

    [Fact]
    public void Chunk_Should_Be_Deterministic()
    {
        var text = string.Join(' ', Enumerable.Range(1, 50).Select(i => $"word{i}"));

        var first = TextChunker.Chunk(text, 7, 2);
        var second = TextChunker.Chunk(text, 7, 2);

        first.Should().Equal(second);
    }

    [Fact]
    public void Chunk_Should_Clamp_NonAdvancing_Overlap_And_Terminate()
    {
        // overlap >= chunk size would never advance the window; the chunker must clamp and finish.
        var text = string.Join(' ', Enumerable.Range(1, 20).Select(i => $"t{i}"));

        var result = TextChunker.Chunk(text, chunkSizeWords: 5, overlapWords: 5);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.Split(' ').Length <= 5);
    }
}
