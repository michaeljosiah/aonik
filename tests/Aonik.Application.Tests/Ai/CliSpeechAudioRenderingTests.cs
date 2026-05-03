using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using FluentAssertions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Verifies that <see cref="TextWriterCliOutputWriter"/>'s known-event
/// renderer turns <c>speech.audio</c> and <c>speech.audio.error</c>
/// CUSTOM events into one-line summaries instead of dumping their JSON
/// (which for <c>speech.audio</c> would print kilobytes of base64 audio
/// per chunk, flooding the terminal during voice-mode dev runs).
/// </summary>
public class CliSpeechAudioRenderingTests
{
    [Fact]
    public void SpeechAudio_Should_RenderOneLineSummary_NotBase64Body()
    {
        var json = """
            {
              "type": "CUSTOM",
              "name": "speech.audio",
              "value": {
                "messageId": "msg-1",
                "chunkIndex": 4,
                "seq": 11,
                "mime": "audio/mpeg",
                "encoding": "base64",
                "data": "AAECAwQFBgcICQoLDA0ODw==",
                "isFinal": false,
                "cached": false,
                "provider": "ElevenLabs",
                "voiceId": "voice-1"
              }
            }
            """;

        var output = TextWriterCliOutputWriter.RenderStreamEvent(
            new AgentStreamEvent("CUSTOM", json, "speech.audio"));

        output.Should().StartWith("[speech.audio]");
        output.Should().Contain("chunk=4");
        output.Should().Contain("seq=11");
        output.Should().Contain("provider=ElevenLabs");
        output.Should().Contain("cached=False");
        output.Should().Contain("final=False");
        output.Should().Contain("bytes=16");

        // The base64 payload must NOT appear — CLI would otherwise flood
        // the terminal with binary on every voice frame.
        output.Should().NotContain("AAECAwQFBgcICQoLDA0ODw==");
    }

    [Fact]
    public void SpeechAudioError_Should_RenderOneLineSummary()
    {
        var json = """
            {
              "type": "CUSTOM",
              "name": "speech.audio.error",
              "value": {
                "messageId": "msg-1",
                "chunkIndex": 7,
                "code": "timeout",
                "message": "TTS synthesis exceeded 5s.",
                "isFinal": true
              }
            }
            """;

        var output = TextWriterCliOutputWriter.RenderStreamEvent(
            new AgentStreamEvent("CUSTOM", json, "speech.audio.error"));

        output.Should().StartWith("[speech.audio.error]");
        output.Should().Contain("chunk=7");
        output.Should().Contain("code=timeout");
        output.Should().Contain("TTS synthesis exceeded 5s.");
    }

    [Fact]
    public void OtherCustomEvent_Should_FallBack_ToRawJsonRendering()
    {
        // Sanity check: events the CLI doesn't recognise still get
        // rendered with the original `[type] json` shape so debugging
        // unknown events isn't blocked.
        var json = """{"type":"CUSTOM","name":"some.future.event","value":{"x":1}}""";
        var output = TextWriterCliOutputWriter.RenderStreamEvent(
            new AgentStreamEvent("CUSTOM", json, "some.future.event"));

        output.Should().StartWith("[CUSTOM]");
        output.Should().Contain("some.future.event");
    }
}
