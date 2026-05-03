using Aonik.Agents.Services;
using FluentAssertions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Pre-flight validation rules for the voice-mode <c>audioFormat</c>
/// request field. Voice runs MUST 400 on unknown abstract formats and
/// MUST 400 on (provider, format) pairs we cannot map.
/// </summary>
public class AudioFormatNegotiationTests
{
    [Theory]
    [InlineData("mp3")]
    [InlineData("opus")]
    [InlineData("wav")]
    public void IsKnownAbstractFormat_Should_AcceptDocumentedAbstractFormats(string format)
    {
        AudioFormatNegotiation.IsKnownAbstractFormat(format).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("flac")]
    [InlineData("aac")]
    [InlineData("mp3_44100_128")]  // provider-specific; must NOT be accepted at the wire.
    [InlineData("MP3")]            // case-sensitive — frontend must send canonical lowercase.
    public void IsKnownAbstractFormat_Should_RejectEverythingElse(string? format)
    {
        AudioFormatNegotiation.IsKnownAbstractFormat(format).Should().BeFalse();
    }

    [Theory]
    [InlineData("ElevenLabs", "mp3", "mp3_44100_128")]
    [InlineData("ElevenLabs", "opus", "opus_48000_64")]
    [InlineData("ElevenLabs", "wav", "pcm_44100")]
    [InlineData("Mistral", "mp3", "mp3")]
    [InlineData("Mistral", "opus", "opus")]
    [InlineData("Mistral", "wav", "wav")]
    public void MapToProviderFormat_Should_ResolveDocumentedPairs(string provider, string abstractFormat, string expected)
    {
        AudioFormatNegotiation.MapToProviderFormat(provider, abstractFormat).Should().Be(expected);
    }

    [Theory]
    [InlineData("Stub", "mp3")]
    [InlineData("ElevenLabs", "flac")]
    [InlineData("ElevenLabs", "")]
    public void MapToProviderFormat_Should_ReturnNull_When_Unsupported(string provider, string abstractFormat)
    {
        AudioFormatNegotiation.MapToProviderFormat(provider, abstractFormat).Should().BeNull();
    }

    [Theory]
    [InlineData("mp3", "audio/mpeg")]
    [InlineData("opus", "audio/opus")]
    [InlineData("wav", "audio/wav")]
    public void MapAbstractToMime_Should_ReturnIanaMimeType(string format, string expected)
    {
        AudioFormatNegotiation.MapAbstractToMime(format).Should().Be(expected);
    }
}
