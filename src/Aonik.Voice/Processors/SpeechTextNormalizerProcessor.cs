using Aonik.SharedKernel.Abstractions.Ai;
using Voxa.Frames;
using Voxa.Processors;

namespace Aonik.Voice.Processors;

/// <summary>
/// Wraps each <see cref="TextFrame"/> through <see cref="ISpeechTextNormalizer"/>
/// before it reaches the TTS processor — strips markdown, expands currency
/// symbols and acronyms, normalises numbers, etc. so synthesised speech is
/// listenable.
///
/// <para>
/// Sits AFTER <c>SentenceAggregator</c> (which consumes
/// <see cref="LlmTextChunkFrame"/>s and emits sentence-sized
/// <see cref="TextFrame"/>s) so normalisation operates on whole sentences,
/// not partial chunks. Normalising mid-token would break currency / acronym /
/// markdown handling. Other frame types pass through untouched.
/// </para>
/// </summary>
public sealed class SpeechTextNormalizerProcessor : FrameProcessor
{
    private readonly ISpeechTextNormalizer _normalizer;

    public SpeechTextNormalizerProcessor(ISpeechTextNormalizer normalizer)
        : base(name: "SpeechTextNormalizer")
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
    }

    protected override async ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
    {
        if (frame is TextFrame text && !string.IsNullOrEmpty(text.Text))
        {
            var normalized = _normalizer.Normalize(text.Text);
            if (!string.IsNullOrEmpty(normalized))
            {
                await PushFrameAsync(text with { Text = normalized }, ct).ConfigureAwait(false);
                return;
            }
            // Empty after normalisation — drop the frame to avoid feeding the TTS
            // an empty utterance that some providers fail on.
            return;
        }

        await PushFrameAsync(frame, ct).ConfigureAwait(false);
    }
}
