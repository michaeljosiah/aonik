using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Verifies the streaming TTS path: frame ordering / IsFinal semantics,
/// AiRun lifecycle (start before provider, complete on success, fail on
/// throw), cache hit / miss / no-op flows, and policy enforcement.
/// </summary>
public class StreamingTextToSpeechServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    [Fact]
    public async Task StreamSynthesizeAsync_Should_Emit_DataFramesThenTerminalFinalFrame()
    {
        // Provider returns 36 KB ⇒ at the 16 KB read window we expect
        // 3 data frames (16 KB, 16 KB, 4 KB) all with IsFinal=false plus
        // one trailing IsFinal=true terminal frame.
        var audio = new byte[36 * 1024];
        new Random(42).NextBytes(audio);
        var fixture = await StreamingTtsTestFixture.CreateAsync(audio);

        var frames = await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Some user request that isn't allowlisted.")));

        frames.Should().HaveCount(4);
        frames[0].IsFinal.Should().BeFalse();
        frames[1].IsFinal.Should().BeFalse();
        frames[2].IsFinal.Should().BeFalse();
        frames[3].IsFinal.Should().BeTrue();

        frames[0].Data.Length.Should().Be(16 * 1024);
        frames[1].Data.Length.Should().Be(16 * 1024);
        frames[2].Data.Length.Should().Be(4 * 1024);
        frames[3].Data.Length.Should().Be(0);

        var assembled = frames.SelectMany(f => f.Data.ToArray()).ToArray();
        assembled.Should().BeEquivalentTo(audio, opts => opts.WithStrictOrdering());

        // Only the first frame carries the AiRunId for audit; subsequent
        // frames omit it to keep the wire payload small.
        frames[0].TtsAiRunId.Should().NotBeNull();
        frames[1].TtsAiRunId.Should().BeNull();
        frames[2].TtsAiRunId.Should().BeNull();
        frames[3].TtsAiRunId.Should().BeNull();

        fixture.AiRunWriter.StartedRuns.Should().Be(1);
        fixture.AiRunWriter.CompletedRuns.Should().Be(1);
        fixture.AiRunWriter.FailedRuns.Should().Be(0);
    }

    [Fact]
    public async Task StreamSynthesizeAsync_Should_ReturnSingleCachedFrame_When_AllowlistedAndCacheHit()
    {
        var fixture = await StreamingTtsTestFixture.CreateAsync(audio: new byte[8]);
        const string allowlistedText = "Sure, let me check that for you.";
        var existingRunId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

        await fixture.Cache.SetAsync(
            BuildKey(allowlistedText, fixture),
            new TtsCacheEntry(
                Audio: new byte[] { 1, 2, 3, 4, 5 },
                ContentType: "audio/mpeg",
                Provider: "ElevenLabs",
                VoiceId: "voice-cached",
                ModelId: "eleven_multilingual_v2",
                OriginalAiRunId: existingRunId,
                CreatedAtUtc: DateTimeOffset.UtcNow));

        var frames = await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest(allowlistedText)));

        frames.Should().ContainSingle();
        frames[0].Cached.Should().BeTrue();
        frames[0].IsFinal.Should().BeTrue();
        frames[0].Data.ToArray().Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
        frames[0].TtsAiRunId.Should().Be(existingRunId);

        // Cache hit short-circuits before any AiRun would be created.
        fixture.AiRunWriter.StartedRuns.Should().Be(0);
        fixture.Provider.SynthesizeCalls.Should().Be(0);
    }

    [Fact]
    public async Task StreamSynthesizeAsync_Should_WriteCache_When_AllowlistedAndCacheMiss()
    {
        var audio = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
        var fixture = await StreamingTtsTestFixture.CreateAsync(audio);
        const string allowlistedText = "Sure, let me check that for you.";

        var frames = await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest(allowlistedText)));

        frames.Last().IsFinal.Should().BeTrue();
        var cached = await fixture.Cache.TryGetAsync(BuildKey(allowlistedText, fixture));
        cached.Should().NotBeNull();
        cached!.Audio.Should().BeEquivalentTo(audio, opts => opts.WithStrictOrdering());
        cached.OriginalAiRunId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StreamSynthesizeAsync_Should_NotWriteCache_When_NotAllowlisted()
    {
        var fixture = await StreamingTtsTestFixture.CreateAsync(audio: new byte[] { 1, 2, 3 });

        await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("This is a personal message about my finances.")));

        fixture.Cache.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task StreamSynthesizeAsync_Should_MarkAiRunFailed_When_ProviderThrows()
    {
        var fixture = await StreamingTtsTestFixture.CreateAsync(audio: Array.Empty<byte>(), throwOnSynthesize: new InvalidOperationException("boom"));

        var act = async () => await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Some text.")));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        fixture.AiRunWriter.StartedRuns.Should().Be(1);
        fixture.AiRunWriter.FailedRuns.Should().Be(1);
        fixture.AiRunWriter.CompletedRuns.Should().Be(0);
    }

    [Fact]
    public void TtsCacheKey_Serialize_Should_BeStableAndIncludeEveryComponent()
    {
        var key = new TtsCacheKey(
            TextHash: "ABC123",
            TenantId: TenantId,
            Provider: "ElevenLabs",
            VoiceId: "voice-1",
            ModelId: "eleven_multilingual_v2",
            Format: "mp3_44100_128",
            Locale: "en-GB");

        var serialized = key.Serialize();

        serialized.Should().StartWith("tts:");
        serialized.Should().Contain(TenantId.ToString("N"));
        serialized.Should().Contain("ElevenLabs");
        serialized.Should().Contain("voice-1");
        serialized.Should().Contain("eleven_multilingual_v2");
        serialized.Should().Contain("mp3_44100_128");
        serialized.Should().Contain("en-GB");
        serialized.Should().Contain("ABC123");
    }

    [Fact]
    public void TtsCacheAllowlist_Contains_Should_MatchKnownPhrasesExactly()
    {
        TtsCacheAllowlist.Contains("I've opened the chat so you can review.").Should().BeTrue();
        TtsCacheAllowlist.Contains("Sure, let me check that for you.").Should().BeTrue();

        // Slight variations and personalised content must NOT match.
        TtsCacheAllowlist.Contains("I've opened the chat so you can review!").Should().BeFalse();
        TtsCacheAllowlist.Contains("Hello Michael, your top transaction is...").Should().BeFalse();
        TtsCacheAllowlist.Contains(string.Empty).Should().BeFalse();
    }

    private static TextToSpeechSynthesisRequest NewRequest(string text) => new(
        SpeechText: text,
        Locale: null,
        ThreadId: "thread-1",
        MessageId: "message-1");

    private static TtsCacheKey BuildKey(string text, StreamingTtsTestFixture fixture) => new(
        TextHash: TtsCacheAllowlist.HashText(SpeechTextNormalizer.Normalize(text)),
        TenantId: TenantId,
        Provider: fixture.Provider.Name,
        VoiceId: fixture.VoiceId,
        ModelId: fixture.ModelId,
        Format: fixture.OutputFormat,
        Locale: fixture.Locale);

    private static async Task<List<TtsAudioFrame>> CollectAsync(IAsyncEnumerable<TtsAudioFrame> source)
    {
        var list = new List<TtsAudioFrame>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    private sealed class StreamingTtsTestFixture
    {
        public required TextToSpeechService Service { get; init; }
        public required FakeTextToSpeechProvider Provider { get; init; }
        public required FakeAiRunWriter AiRunWriter { get; init; }
        public required InMemoryTtsCache Cache { get; init; }
        public required string VoiceId { get; init; }
        public required string ModelId { get; init; }
        public required string OutputFormat { get; init; }
        public required string Locale { get; init; }

        public static async Task<StreamingTtsTestFixture> CreateAsync(byte[] audio, Exception? throwOnSynthesize = null)
        {
            const string voiceId = "voice-1";
            const string modelId = "eleven_multilingual_v2";
            const string outputFormat = "mp3_44100_128";
            const string locale = "en-GB";

            var provider = new FakeTextToSpeechProvider(audio, throwOnSynthesize);
            var settings = new TextToSpeechSettings(
                Enabled: true,
                FallbackToNativeOnFailure: false,
                DefaultProfile: new TextToSpeechVoiceProfile(
                    Provider: provider.Name,
                    VoiceId: voiceId,
                    ModelId: modelId,
                    Locale: locale,
                    OutputFormat: outputFormat,
                    ProviderOptions: new Dictionary<string, string?>()),
                Policy: new TextToSpeechPolicy(
                    MaxCharactersPerUtterance: 1000,
                    MaxRequestsPerMinutePerUser: 60,
                    MonthlyCharacterBudget: null));

            var settingsService = new FakeSettingsService(settings);
            var credentialResolver = new FakeCredentialResolver();
            var aiRunWriter = new FakeAiRunWriter();
            var rateLimiter = new AlwaysOpenRateLimiter();
            var cache = new InMemoryTtsCache();
            var tenantProvider = new FixedTenantProvider { TenantId = TenantId };
            var userProvider = new FixedUserProvider { UserId = UserId };

            var service = new TextToSpeechService(
                providers: new ITextToSpeechProvider[] { provider },
                credentialResolver: credentialResolver,
                settingsService: settingsService,
                tenantProvider: tenantProvider,
                currentUserProvider: userProvider,
                aiRunWriter: aiRunWriter,
                rateLimiter: rateLimiter,
                ttsCache: cache,
                logger: NullLogger<TextToSpeechService>.Instance);

            return await Task.FromResult(new StreamingTtsTestFixture
            {
                Service = service,
                Provider = provider,
                AiRunWriter = aiRunWriter,
                Cache = cache,
                VoiceId = voiceId,
                ModelId = modelId,
                OutputFormat = outputFormat,
                Locale = locale,
            });
        }
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; init; }
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private sealed class FixedUserProvider : ICurrentUserProvider
    {
        public Guid? UserId { get; init; }
        public Guid? GetCurrentUserId() => UserId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = UserId ?? Guid.Empty; return UserId.HasValue; }
    }

    private sealed class FakeSettingsService(TextToSpeechSettings settings) : ITenantTextToSpeechSettingsService
    {
        public Task<TextToSpeechSettings> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task<TextToSpeechSettings> SaveCurrentAsync(TextToSpeechSettings updated, CancellationToken cancellationToken = default) =>
            Task.FromResult(updated);
    }

    private sealed class FakeCredentialResolver : ITextToSpeechCredentialResolver
    {
        public Task<TextToSpeechProviderCredentialResolution> ResolveAsync(string providerName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TextToSpeechProviderCredentialResolution(
                Provider: providerName,
                ApiKey: "test-api-key",
                Source: "test",
                HasCredential: true,
                IsTenantOverride: false));
    }

    private sealed class AlwaysOpenRateLimiter : ITextToSpeechRateLimiter
    {
        public bool TryConsume(Guid tenantId, Guid userId, int maxPerMinutePerUser, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    private sealed class FakeAiRunWriter : IAiRunWriter
    {
        public int StartedRuns { get; private set; }
        public int CompletedRuns { get; private set; }
        public int FailedRuns { get; private set; }
        public List<Guid> Runs { get; } = new();

        public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default)
        {
            StartedRuns++;
            var id = Guid.NewGuid();
            Runs.Add(id);
            return Task.FromResult(id);
        }

        public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default)
        {
            CompletedRuns++;
            return Task.CompletedTask;
        }

        public Task MarkRunCompletedWithMetricsAsync(Guid aiRunId, int tokensUsed, int latencyMs, decimal costEstimate, string? outputRef = null, CancellationToken cancellationToken = default)
        {
            CompletedRuns++;
            return Task.CompletedTask;
        }

        public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default)
        {
            FailedRuns++;
            return Task.CompletedTask;
        }

        public Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());
    }

    private sealed class FakeTextToSpeechProvider(byte[] audio, Exception? throwOnSynthesize = null) : ITextToSpeechProvider
    {
        public string Name => "FakeProvider";
        public int SynthesizeCalls { get; private set; }

        public Task<TextToSpeechProviderStreamResult> SynthesizeAsync(TextToSpeechProviderRequest request, CancellationToken cancellationToken = default)
        {
            SynthesizeCalls++;
            if (throwOnSynthesize is not null) throw throwOnSynthesize;

            return Task.FromResult(new TextToSpeechProviderStreamResult(
                AudioStream: new MemoryStream(audio, writable: false),
                ContentType: "audio/mpeg",
                Provider: Name,
                VoiceId: request.VoiceId,
                ModelId: request.ModelId));
        }

        public Task<IReadOnlyList<TextToSpeechVoiceOption>> GetVoicesAsync(string? apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TextToSpeechVoiceOption>>(Array.Empty<TextToSpeechVoiceOption>());
    }

    private sealed class InMemoryTtsCache : ITtsCache
    {
        public Dictionary<string, TtsCacheEntry> Entries { get; } = new();

        public bool IsAllowlisted(string normalizedText) => TtsCacheAllowlist.Contains(normalizedText);

        public ValueTask<TtsCacheEntry?> TryGetAsync(TtsCacheKey key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Entries.TryGetValue(key.Serialize(), out var entry) ? entry : null);

        public ValueTask SetAsync(TtsCacheKey key, TtsCacheEntry entry, CancellationToken cancellationToken = default)
        {
            Entries[key.Serialize()] = entry;
            return ValueTask.CompletedTask;
        }
    }
}
