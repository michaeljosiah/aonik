using Aonik.Agents.Services;
using FluentAssertions;

namespace Aonik.Application.Tests.Ai;

public class SpeechStreamBufferTests
{
    [Fact]
    public void TryPopSentence_Should_EmitTerminalSentenceAtBufferEnd()
    {
        var buffer = new SpeechStreamBuffer();
        buffer.Append("One sec - pulling that up.");

        var popped = buffer.TryPopSentence(out var rawChunk);

        popped.Should().BeTrue();
        rawChunk.Should().Be("One sec - pulling that up.");
        buffer.NextChunkIndex.Should().Be(1);
        buffer.FlushRemaining().Should().BeNull();
    }

    [Fact]
    public void TryPopSentence_Should_EmitTerminalSentenceWithTrailingWhitespace()
    {
        var buffer = new SpeechStreamBuffer();
        buffer.Append("Done. ");

        var popped = buffer.TryPopSentence(out var rawChunk);

        popped.Should().BeTrue();
        rawChunk.Should().Be("Done.");
        buffer.FlushRemaining().Should().BeNull();
    }

    [Fact]
    public void TryPopSentence_Should_NotSplitDecimalAtBufferEnd()
    {
        var buffer = new SpeechStreamBuffer();
        buffer.Append("Your balance is 12.50");

        var popped = buffer.TryPopSentence(out var rawChunk);

        popped.Should().BeFalse();
        rawChunk.Should().BeEmpty();
        buffer.NextChunkIndex.Should().Be(0);
    }

    [Fact]
    public void TryPopSentence_Should_PopFirstSentenceWhenMoreTextFollows()
    {
        var buffer = new SpeechStreamBuffer();
        buffer.Append("First. Second");

        var popped = buffer.TryPopSentence(out var rawChunk);

        var remaining = buffer.FlushRemaining();

        popped.Should().BeTrue();
        rawChunk.Should().Be("First.");
        remaining.Should().Be("Second");
        buffer.NextChunkIndex.Should().Be(2);
    }
}
