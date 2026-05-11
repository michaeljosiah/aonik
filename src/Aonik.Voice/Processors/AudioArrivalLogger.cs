using Microsoft.Extensions.Logging;
using Voxa.Frames;
using Voxa.Processors;
using Voxa.Speech;

namespace Aonik.Voice.Processors;

/// <summary>
/// Diagnostic pass-through that logs how much audio is reaching the pipeline,
/// once per second, at <c>information</c> level. Modelled on the equivalent
/// processor in <c>Voxa.Samples.AspNetServer/Program.cs</c> — and added for the
/// same reason: when a voice WebSocket "doesn't work", the first question is
/// always "is audio even arriving at all, and at what sample rate?". With this
/// logger inserted right after the source, container logs answer that without
/// needing to attach a debugger.
///
/// <para>
/// Output shape (one line per second):
/// <code>audio inbound: 20 frames / 32000 B / peak RMS 0.1659 (sample rate 16000 Hz)</code>
/// — 20 frames matches the browser worklet's 50 ms chunking, 32 000 B is 16 000 Hz × 2 B × 1 s,
/// peak RMS 0.16+ indicates speech-level energy (silence is &lt; 0.01).
/// </para>
///
/// <para>
/// Non-audio frames pass through unchanged. Drop the processor entirely once the
/// pipeline is proven on production traffic — it's safe to leave in (one log line
/// per second is cheap) but it's also not load-bearing.
/// </para>
/// </summary>
public sealed class AudioArrivalLogger : FrameProcessor
{
    private readonly ILogger _logger;
    private int _framesThisSecond;
    private long _bytesThisSecond;
    private double _peakRmsThisSecond;
    private DateTime _windowStart = DateTime.UtcNow;

    public AudioArrivalLogger(ILogger logger)
        : base("AudioArrivalLogger")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
    {
        if (frame is AudioRawFrame audio)
        {
            _framesThisSecond++;
            _bytesThisSecond += audio.Pcm.Length;
            _peakRmsThisSecond = Math.Max(
                _peakRmsThisSecond,
                SilenceGateProcessor.ComputeRms(audio.Pcm.Span));

            var elapsed = DateTime.UtcNow - _windowStart;
            if (elapsed >= TimeSpan.FromSeconds(1))
            {
                _logger.LogInformation(
                    "audio inbound: {Frames} frames / {Bytes} B / peak RMS {Rms:F4} (sample rate {Sr} Hz)",
                    _framesThisSecond,
                    _bytesThisSecond,
                    _peakRmsThisSecond,
                    audio.SampleRate);
                _framesThisSecond = 0;
                _bytesThisSecond = 0;
                _peakRmsThisSecond = 0;
                _windowStart = DateTime.UtcNow;
            }
        }
        await PushFrameAsync(frame, ct).ConfigureAwait(false);
    }
}
