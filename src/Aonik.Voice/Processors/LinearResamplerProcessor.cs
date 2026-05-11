using System.Buffers.Binary;
using Voxa.Frames;
using Voxa.Processors;

namespace Aonik.Voice.Processors;

/// <summary>
/// Linear-interpolation resampler for 16-bit signed LE PCM. Used by the composite voice
/// path (<c>OpenAI Realtime</c> / <c>Azure Voice Live</c>) to upsample the 16 kHz audio
/// our mobile + web clients capture to the 24 kHz both APIs require on their input audio
/// buffer.
///
/// <para>
/// Quality is "good enough for speech" — linear interpolation introduces high-frequency
/// roll-off but is inaudible on voice content compared to a proper sinc / polyphase
/// filter. The Realtime API runs its own perceptual codec downstream, so any artefacts
/// we add are washed out by the time the model sees them. Total CPU cost on a Container
/// App is well under 1 % of one vCPU per session at 16→24 kHz.
/// </para>
///
/// <para>
/// State is kept across frames so chunks split mid-sample interpolate continuously —
/// we remember the last input sample so the first output sample of the next frame
/// interpolates against the correct neighbour instead of jumping back to silence.
/// </para>
///
/// <para>
/// Frames whose <see cref="AudioRawFrame.SampleRate"/> doesn't match the configured
/// input rate are forwarded unchanged. This lets the processor sit in any pipeline
/// without breaking, e.g. start-of-session control frames or pre-resampled audio.
/// </para>
/// </summary>
public sealed class LinearResamplerProcessor : FrameProcessor
{
    private readonly int _inputSampleRate;
    private readonly int _outputSampleRate;
    private readonly double _ratio;

    // Carry-over state from the previous frame so interpolation across frame
    // boundaries is continuous. Reset on Start so a new session starts clean.
    private short _previousSample;
    private bool _hasPreviousSample;

    /// <summary>Build a resampler from <paramref name="inputSampleRate"/> to <paramref name="outputSampleRate"/>.</summary>
    public LinearResamplerProcessor(int inputSampleRate, int outputSampleRate)
        : base($"Resample{inputSampleRate}to{outputSampleRate}")
    {
        if (inputSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(inputSampleRate));
        if (outputSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(outputSampleRate));
        _inputSampleRate = inputSampleRate;
        _outputSampleRate = outputSampleRate;
        _ratio = (double)outputSampleRate / inputSampleRate;
    }

    protected override ValueTask OnStartAsync(StartFrame f, CancellationToken ct)
    {
        _previousSample = 0;
        _hasPreviousSample = false;
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
    {
        if (frame is AudioRawFrame audio && audio.SampleRate == _inputSampleRate)
        {
            // Mono assumption mirrors the rest of the AONIK voice pipeline; all four
            // chained TTS engines and both composite engines speak signed-16-LE mono.
            // If a multi-channel frame ever lands here, fall through to the passthrough
            // branch rather than corrupting the layout.
            if (audio.Channels == 1)
            {
                var resampled = Resample16to(audio.Pcm.Span);
                await PushFrameAsync(
                    new AudioRawFrame(resampled, _outputSampleRate, audio.Channels), ct).ConfigureAwait(false);
                return;
            }
        }

        await PushFrameAsync(frame, ct).ConfigureAwait(false);
    }

    private ReadOnlyMemory<byte> Resample16to(ReadOnlySpan<byte> pcm)
    {
        var inSamples = pcm.Length / 2;
        if (inSamples == 0) return ReadOnlyMemory<byte>.Empty;

        // Read int16 samples once into a heap array to avoid byte-shifting in the hot
        // inner loop. Avoiding stackalloc keeps the ternary-vs-Span subtleties out of
        // scope; allocations here are still small (typical 20 ms @ 16 kHz frame = 320
        // samples = 640 bytes) and short-lived.
        var input = new short[inSamples];
        for (var i = 0; i < inSamples; i++)
        {
            input[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * 2, 2));
        }

        // Output sample count = ceil(inSamples * ratio). +1 covers rounding so we never
        // truncate the tail. Excess bytes (1 sample max) are trimmed before returning.
        var outSamples = (int)Math.Ceiling(inSamples * _ratio) + 1;
        var output = new byte[outSamples * 2];
        var written = 0;

        // Walk the output grid; at each step compute the fractional input position
        // and linearly interpolate between the two nearest input samples. Carry-over
        // from the previous frame is used when we'd otherwise read index -1.
        for (var n = 0; n < outSamples; n++)
        {
            var inPos = n / _ratio;
            var i0 = (int)Math.Floor(inPos);
            var frac = inPos - i0;

            short s0;
            short s1;

            if (i0 < 0)
            {
                // Shouldn't happen with n >= 0, but defensive.
                s0 = _hasPreviousSample ? _previousSample : (short)0;
                s1 = input[0];
            }
            else if (i0 >= inSamples)
            {
                break;
            }
            else if (i0 == 0 && _hasPreviousSample)
            {
                s0 = _previousSample;
                s1 = input[0];
                // The first output sample interpolates between the previous frame's
                // last sample and this frame's first sample. After that, fall through
                // to the normal in-frame logic.
            }
            else if (i0 + 1 >= inSamples)
            {
                // Last input sample of the frame — no neighbour available within this
                // frame. Hold; the next frame's carry-over will pick up correctly.
                s0 = input[i0];
                s1 = input[i0];
            }
            else
            {
                s0 = input[i0];
                s1 = input[i0 + 1];
            }

            var interpolated = (int)Math.Round(s0 + (s1 - s0) * frac);
            if (interpolated > short.MaxValue) interpolated = short.MaxValue;
            if (interpolated < short.MinValue) interpolated = short.MinValue;

            BinaryPrimitives.WriteInt16LittleEndian(
                output.AsSpan(written, 2),
                (short)interpolated);
            written += 2;
        }

        _previousSample = input[inSamples - 1];
        _hasPreviousSample = true;

        return new ReadOnlyMemory<byte>(output, 0, written);
    }
}
