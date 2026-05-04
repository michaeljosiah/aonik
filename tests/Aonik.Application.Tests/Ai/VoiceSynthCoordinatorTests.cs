using System.Diagnostics;
using System.Runtime.CompilerServices;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

public class VoiceSynthCoordinatorTests
{
    [Fact]
    public async Task StartChunkSynthesis_Should_PassNegotiatedProviderFormatToStreamingTts()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var streamingTts = new CapturingStreamingTextToSpeechService();
        await using var services = new ServiceCollection()
            .AddSingleton<IStreamingTextToSpeechService>(streamingTts)
            .BuildServiceProvider();

        await using var writer = new AguiResponseWriter(
            context.Response,
            voiceMode: true,
            wallClock: Stopwatch.StartNew());
        await using var coordinator = new VoiceSynthCoordinator(
            serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>(),
            writer: writer,
            providerFormat: "opus_48000_64",
            mime: "audio/opus",
            logger: NullLogger.Instance);

        coordinator.StartChunkSynthesis(
            messageId: "message-1",
            chunkIndex: 0,
            speechText: "Hello there.",
            threadId: "thread-1",
            runCancellation: CancellationToken.None);

        await coordinator.WaitForAllSynthesisAsync();
        writer.CompleteAudioInput();
        await writer.WaitForAudioDrainAsync();

        streamingTts.Requests.Should().ContainSingle();
        streamingTts.Requests[0].VoiceProfileOverride.Should().NotBeNull();
        streamingTts.Requests[0].VoiceProfileOverride!.OutputFormat.Should().Be("opus_48000_64");
    }

    private sealed class CapturingStreamingTextToSpeechService : IStreamingTextToSpeechService
    {
        public List<TextToSpeechSynthesisRequest> Requests { get; } = new();

        public async IAsyncEnumerable<TtsAudioFrame> StreamSynthesizeAsync(
            TextToSpeechSynthesisRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            await Task.Yield();
            yield return new TtsAudioFrame(
                Data: new byte[] { 1, 2, 3 },
                ContentType: "audio/opus",
                Provider: "FakeProvider",
                VoiceId: "voice-1",
                IsFinal: false,
                Cached: false,
                TtsAiRunId: Guid.NewGuid());
            yield return new TtsAudioFrame(
                Data: ReadOnlyMemory<byte>.Empty,
                ContentType: "audio/opus",
                Provider: "FakeProvider",
                VoiceId: "voice-1",
                IsFinal: true,
                Cached: false,
                TtsAiRunId: null);
        }
    }
}
