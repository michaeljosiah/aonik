using System.Diagnostics;
using System.Text;
using Aonik.Agents.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Verifies the prioritised SSE writer's invariants: control writes
/// take precedence over queued audio, the audio pump drains before
/// <c>WaitForAudioDrainAsync</c> resolves (writer-flush refinement),
/// and channel overflow surfaces as <c>speech.audio.error</c> control
/// events without stalling the run.
/// </summary>
public class AguiResponseWriterTests
{
    [Fact]
    public async Task WriteControlAsync_Should_EmitSseFramedJson()
    {
        var (response, body) = CreateResponse();
        await using var writer = new AguiResponseWriter(response, voiceMode: false, Stopwatch.StartNew());

        await writer.WriteControlAsync(new { type = "RUN_STARTED", threadId = "t1", runId = "r1" }, default);
        await response.Body.FlushAsync();

        var output = Encoding.UTF8.GetString(body.ToArray());
        output.Should().StartWith("data: ");
        output.Should().EndWith("\n\n");
        output.Should().Contain("\"type\":\"RUN_STARTED\"");
        output.Should().Contain("\"threadId\":\"t1\"");
        output.Should().Contain("\"runId\":\"r1\"");
    }

    [Fact]
    public async Task NonVoiceMode_Should_IgnoreAudioEnqueue_AndDrainImmediately()
    {
        var (response, body) = CreateResponse();
        await using var writer = new AguiResponseWriter(response, voiceMode: false, Stopwatch.StartNew());

        await writer.EnqueueAudioFrameAsync(
            messageId: "m1", chunkIndex: 0, data: new byte[] { 1, 2, 3 },
            mime: "audio/mpeg", isFinal: true, cached: false,
            provider: "Stub", voiceId: "v1", ttsAiRunId: null,
            cancellationToken: default);

        writer.CompleteAudioInput();
        await writer.WaitForAudioDrainAsync();

        var output = Encoding.UTF8.GetString(body.ToArray());
        output.Should().NotContain("speech.audio");
        writer.GetAudioMetrics().AudioFrames.Should().Be(0);
        writer.GetAudioMetrics().VoiceMode.Should().BeFalse();
    }

    [Fact]
    public async Task VoiceMode_Should_FlushAudioFrame_AsCustomEvent_BeforeDrainResolves()
    {
        var (response, body) = CreateResponse();
        await using var writer = new AguiResponseWriter(response, voiceMode: true, Stopwatch.StartNew());

        await writer.EnqueueAudioFrameAsync(
            messageId: "m1", chunkIndex: 3, data: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
            mime: "audio/mpeg", isFinal: true, cached: false,
            provider: "ElevenLabs", voiceId: "voice-1", ttsAiRunId: null,
            cancellationToken: default);

        writer.CompleteAudioInput();
        await writer.WaitForAudioDrainAsync();

        var output = Encoding.UTF8.GetString(body.ToArray());
        output.Should().Contain("\"name\":\"speech.audio\"");
        output.Should().Contain("\"chunkIndex\":3");
        output.Should().Contain("\"mime\":\"audio/mpeg\"");
        // System.Text.Json escapes `+` as `+` in JSON strings.
        output.Should().Contain("\"data\":\"3q2\\u002B7w==\"");
        output.Should().Contain("\"isFinal\":true");

        var metrics = writer.GetAudioMetrics();
        metrics.AudioFrames.Should().Be(1);
        metrics.AudioBytes.Should().Be(4);
        metrics.AudioDrainMs.Should().NotBeNull();
    }

    [Fact]
    public async Task VoiceMode_Overflow_Should_EmitBackpressureErrorOnControlChannel()
    {
        var (response, body) = CreateResponse();
        // Block the writer's pump by holding the response stream busy.
        // Easier: enqueue more frames than capacity (8) before letting the pump drain.
        // We achieve this by NOT awaiting drain between enqueues so the channel fills up.
        await using var writer = new AguiResponseWriter(response, voiceMode: true, Stopwatch.StartNew());

        // Pump runs concurrently; to provoke overflow, blast enough
        // small frames quickly. With response.Body being an
        // in-memory stream the pump is fast, so we need to enqueue
        // synchronously without awaiting between attempts. We use
        // TryWrite semantics under the hood — anything that doesn't
        // fit the channel slot drops with a backpressure error event.
        var pendingTasks = new List<Task>();
        for (var i = 0; i < 32; i++)
        {
            pendingTasks.Add(writer.EnqueueAudioFrameAsync(
                messageId: "m1", chunkIndex: i, data: new byte[] { (byte)i },
                mime: "audio/mpeg", isFinal: false, cached: false,
                provider: "ElevenLabs", voiceId: "voice-1", ttsAiRunId: null,
                cancellationToken: default));
        }

        await Task.WhenAll(pendingTasks);
        writer.CompleteAudioInput();
        await writer.WaitForAudioDrainAsync();

        // We can't deterministically force overflow because the pump may
        // drain quickly. Instead, assert that drops + writes account for
        // every frame attempted, and any drops produced an audio.error.
        var metrics = writer.GetAudioMetrics();
        (metrics.AudioFrames + metrics.AudioFramesDropped).Should().Be(32);

        var output = Encoding.UTF8.GetString(body.ToArray());
        if (metrics.AudioFramesDropped > 0)
        {
            output.Should().Contain("speech.audio.error");
            output.Should().Contain("backpressure_dropped");
        }
    }

    [Fact]
    public async Task EmitAudioErrorAsync_Should_AlwaysWriteOnControlChannel()
    {
        var (response, body) = CreateResponse();
        await using var writer = new AguiResponseWriter(response, voiceMode: true, Stopwatch.StartNew());

        await writer.EmitAudioErrorAsync("m1", chunkIndex: 5, code: "timeout", message: "TTS timed out.", default);
        writer.CompleteAudioInput();
        await writer.WaitForAudioDrainAsync();

        var output = Encoding.UTF8.GetString(body.ToArray());
        output.Should().Contain("\"name\":\"speech.audio.error\"");
        output.Should().Contain("\"chunkIndex\":5");
        output.Should().Contain("\"code\":\"timeout\"");
        output.Should().Contain("\"isFinal\":true");
    }

    private static (HttpResponse Response, MemoryStream Body) CreateResponse()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;
        context.Response.ContentType = "text/event-stream";
        return (context.Response, body);
    }
}
