using System.Buffers.Binary;
using System.Threading.Channels;
using Aonik.Voice.Processors;
using FluentAssertions;
using Voxa.Frames;
using Voxa.Processors;

namespace Aonik.Voice.Tests.Pipeline;

/// <summary>
/// Unit tests for <see cref="LinearResamplerProcessor"/> — the 16→24 kHz upsampler used by the
/// composite voice path. The shape of the output is what matters here (sample count, frames
/// passed through unchanged when the rate doesn't match); audio quality is a perceptual concern
/// that's exercised by the end-to-end Voice Live / OpenAI Realtime integration tests.
/// </summary>
public class LinearResamplerProcessorTests
{
    [Fact]
    public async Task Resampler_Upsamples_16kHz_To_24kHz_With_Expected_Output_Length()
    {
        // 160 samples @ 16 kHz = 10 ms of audio. Output should be ~240 samples @ 24 kHz.
        await using var harness = new ProcessorHarness(new LinearResamplerProcessor(16000, 24000));

        await harness.SendAsync(new AudioRawFrame(
            BuildSineWavePcm(samples: 160, frequencyHz: 440, sampleRate: 16000), 16000, 1));

        var resampled = await harness.WaitForFirstAudioAsync();
        var outputSamples = resampled.Pcm.Length / 2;

        // Linear interpolation with ratio 1.5 produces ceil(160 * 1.5) = 240 samples; the
        // implementation walks the output grid until i0 >= inSamples, so we expect exactly
        // 240 samples on a 160-sample input (and never more than 240+1 due to the +1
        // headroom for rounding).
        outputSamples.Should().BeInRange(238, 241);
        resampled.SampleRate.Should().Be(24000);
        resampled.Channels.Should().Be(1);
    }

    [Fact]
    public async Task Resampler_Passes_Through_Frames_With_Mismatched_Sample_Rate()
    {
        await using var harness = new ProcessorHarness(new LinearResamplerProcessor(16000, 24000));

        await harness.SendAsync(new AudioRawFrame(new byte[40], SampleRate: 8000, Channels: 1));

        var emitted = await harness.WaitForFirstAudioAsync();
        emitted.SampleRate.Should().Be(8000);
        emitted.Pcm.Length.Should().Be(40);
    }

    [Fact]
    public async Task Resampler_Passes_Through_Multi_Channel_Frames_Untouched()
    {
        // Two-channel audio falls through the mono assumption — the processor mustn't try
        // to upsample interleaved stereo as if it were mono.
        await using var harness = new ProcessorHarness(new LinearResamplerProcessor(16000, 24000));

        await harness.SendAsync(new AudioRawFrame(new byte[40], SampleRate: 16000, Channels: 2));

        var emitted = await harness.WaitForFirstAudioAsync();
        emitted.SampleRate.Should().Be(16000);
        emitted.Channels.Should().Be(2);
    }

    private static byte[] BuildSineWavePcm(int samples, double frequencyHz, int sampleRate)
    {
        var output = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / sampleRate;
            var value = (short)(Math.Sin(2 * Math.PI * frequencyHz * t) * short.MaxValue * 0.5);
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(i * 2, 2), value);
        }
        return output;
    }

    /// <summary>
    /// Lifecycle wrapper that puts a processor and a capture sink together, starts both
    /// background drain tasks, and tears everything down on dispose. Voxa's
    /// <see cref="FrameProcessor"/> needs <see cref="FrameProcessor.Start"/> to be called
    /// explicitly — without it, <c>QueueFrameAsync</c> just fills the bounded channel and
    /// the test deadlocks because no consumer ever drains it.
    /// </summary>
    private sealed class ProcessorHarness : IAsyncDisposable
    {
        private readonly FrameProcessor _processor;
        private readonly CaptureSink _sink;

        public ProcessorHarness(FrameProcessor processor)
        {
            _processor = processor;
            _sink = new CaptureSink();
            _processor.Link(_sink);
            _processor.Start();
            _sink.Start();
            // StartFrame is required before the processor sees any data frames — Voxa's
            // FrameProcessor runs OnStartAsync as the first item off the data channel.
            _ = _processor.QueueFrameAsync(new StartFrame());
        }

        public ValueTask SendAsync(Frame frame) => _processor.QueueFrameAsync(frame);

        public Task<AudioRawFrame> WaitForFirstAudioAsync() => _sink.WaitForFirstAudioAsync();

        public async ValueTask DisposeAsync()
        {
            // EndFrame triggers the data loop to exit; DisposeAsync drains it.
            try { await _processor.QueueFrameAsync(new EndFrame()); } catch { /* shutdown race */ }
            await _processor.DisposeAsync();
            await _sink.DisposeAsync();
        }
    }

    /// <summary>
    /// Minimal in-test sink that captures audio frames the resampler emits.
    /// </summary>
    private sealed class CaptureSink : FrameProcessor
    {
        private readonly Channel<AudioRawFrame> _audio = Channel.CreateUnbounded<AudioRawFrame>();

        public CaptureSink() : base("CaptureSink") { }

        protected override async ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
        {
            if (frame is AudioRawFrame audio)
            {
                await _audio.Writer.WriteAsync(audio, ct);
            }
        }

        public async Task<AudioRawFrame> WaitForFirstAudioAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await _audio.Reader.ReadAsync(cts.Token);
        }
    }
}
