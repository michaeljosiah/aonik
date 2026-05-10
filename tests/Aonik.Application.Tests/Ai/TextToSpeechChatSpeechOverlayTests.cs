using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 024 Phase C.2 verification — when <see cref="IChatSpeechSettingsService"/> +
/// <see cref="ISpeechProviderLibraryService"/> are wired, the active TTS provider id from
/// the new singleton overlays the legacy <see cref="TextToSpeechSettings.DefaultProfile"/>
/// before the per-request override and rate-limit checks run.
/// </summary>
public class TextToSpeechChatSpeechOverlayTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    [Fact]
    public async Task StreamSynthesize_Should_OverrideLegacyProfile_When_ChatSpeechActiveProviderIsActiveTts()
    {
        // Legacy default = "LegacyProvider/voice-legacy"; chat speech active = ElevenLabs/voice-overlay.
        // Overlay must win — the captured provider request should carry voice-overlay.
        var fixture = await Fixture.CreateAsync(
            chatSpeechProviderId: "f00d0000-0000-0000-0000-000000000001",
            chatSpeechProviderResolved: BuildElevenLabsProvider(
                id: "f00d0000-0000-0000-0000-000000000001",
                voiceId: "voice-overlay",
                modelId: "eleven_multilingual_v2"));

        await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Standard chat reply.")));

        fixture.LegacyProvider.SynthesizeCalls.Should().Be(0, "the overlay should re-route synthesis to the ElevenLabs provider");
        fixture.OverlayProvider.SynthesizeCalls.Should().Be(1);
        fixture.OverlayProvider.LastRequest!.VoiceId.Should().Be("voice-overlay");
        fixture.OverlayProvider.LastRequest!.ModelId.Should().Be("eleven_multilingual_v2");
    }

    [Fact]
    public async Task StreamSynthesize_Should_FallBackToLegacy_When_OverlayProviderResolutionReturnsNull()
    {
        // ActiveTtsProviderId points at a provider that's been deleted/soft-deleted —
        // service must log + degrade gracefully back to the legacy default profile rather
        // than failing the synthesis call.
        var fixture = await Fixture.CreateAsync(
            chatSpeechProviderId: "f00d0000-0000-0000-0000-000000000099",
            chatSpeechProviderResolved: null);

        await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Standard chat reply.")));

        fixture.LegacyProvider.SynthesizeCalls.Should().Be(1);
        fixture.OverlayProvider.SynthesizeCalls.Should().Be(0);
    }

    [Fact]
    public async Task StreamSynthesize_Should_FallBackToLegacy_When_OverlayProviderIsDisabled()
    {
        var fixture = await Fixture.CreateAsync(
            chatSpeechProviderId: "f00d0000-0000-0000-0000-000000000002",
            chatSpeechProviderResolved: BuildElevenLabsProvider(
                id: "f00d0000-0000-0000-0000-000000000002",
                voiceId: "voice-overlay",
                modelId: null,
                status: SpeechProviderStatus.Disabled));

        await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Standard chat reply.")));

        fixture.LegacyProvider.SynthesizeCalls.Should().Be(1);
        fixture.OverlayProvider.SynthesizeCalls.Should().Be(0);
    }

    [Fact]
    public async Task StreamSynthesize_Should_RejectSynthesis_When_ChatSpeechIsExplicitlyDisabled()
    {
        // Even when legacy.Enabled = true, an explicit disable in the new ChatSpeechSettings
        // must take precedence — the user toggled off in the new UI and expects silence.
        var fixture = await Fixture.CreateAsync(
            chatSpeechProviderId: null,
            chatSpeechProviderResolved: null,
            chatSpeechEnabled: false);

        var act = async () => await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Standard chat reply.")));

        await act.Should()
            .ThrowAsync<TextToSpeechPolicyViolationException>()
            .Where(ex => ex.Code == "tts_disabled");
    }

    [Fact]
    public async Task StreamSynthesize_Should_UseLegacyProfile_When_ChatSpeechHasNoActiveProviderId()
    {
        var fixture = await Fixture.CreateAsync(
            chatSpeechProviderId: null,
            chatSpeechProviderResolved: null);

        await CollectAsync(fixture.Service.StreamSynthesizeAsync(NewRequest("Standard chat reply.")));

        fixture.LegacyProvider.SynthesizeCalls.Should().Be(1);
        fixture.OverlayProvider.SynthesizeCalls.Should().Be(0);
    }

    private static SpeechProvider BuildElevenLabsProvider(
        string id,
        string voiceId,                   // kept on the test signature for readability — it
        string? modelId,                  // flows to the ChatSpeechSettings, not the provider
        SpeechProviderStatus status = SpeechProviderStatus.Active) =>
        new(
            Id: id,
            DisplayName: "Test ElevenLabs",
            Type: SpeechProviderType.Tts,
            Vendor: "elevenlabs",
            Config: new ElevenLabsTtsConfig(
                DefaultModelId: modelId,
                DefaultStability: null,
                DefaultSimilarityBoost: null,
                DefaultOptimizeStreamingLatency: null),
            Status: status,
            HasApiKey: false,
            IsBuiltIn: false,
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CreatedByUserId: null,
            LastUpdatedByUserId: null);

    private static TextToSpeechSynthesisRequest NewRequest(string text) => new(
        SpeechText: text,
        Locale: null,
        ThreadId: "thread-1",
        MessageId: "message-1");

    private static async Task<List<TtsAudioFrame>> CollectAsync(IAsyncEnumerable<TtsAudioFrame> source)
    {
        var list = new List<TtsAudioFrame>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    private sealed class Fixture
    {
        public required TextToSpeechService Service { get; init; }
        public required FakeProvider LegacyProvider { get; init; }
        public required FakeProvider OverlayProvider { get; init; }

        public static Task<Fixture> CreateAsync(
            string? chatSpeechProviderId,
            SpeechProvider? chatSpeechProviderResolved,
            bool chatSpeechEnabled = true)
        {
            var legacyProvider = new FakeProvider("LegacyProvider");
            var overlayProvider = new FakeProvider("ElevenLabs");

            var legacySettings = new TextToSpeechSettings(
                Enabled: true,
                FallbackToNativeOnFailure: false,
                DefaultProfile: new TextToSpeechVoiceProfile(
                    Provider: "LegacyProvider",
                    VoiceId: "voice-legacy",
                    ModelId: "model-legacy",
                    Locale: "en-GB",
                    OutputFormat: "mp3_44100_128",
                    ProviderOptions: new Dictionary<string, string?>()),
                Policy: new TextToSpeechPolicy(
                    MaxCharactersPerUtterance: 1000,
                    MaxRequestsPerMinutePerUser: 60,
                    MonthlyCharacterBudget: null));

            var chat = new ChatSpeechSettings(
                ActiveTtsProviderId: chatSpeechProviderId,
                ActiveTtsVoiceId: chatSpeechProviderResolved is null ? null : "voice-overlay",
                ActiveTtsModelId: null,
                Enabled: chatSpeechEnabled,
                AutoPlay: false,
                ShowSpeakButton: true,
                RatePercent: 100,
                UpdatedAt: DateTimeOffset.UtcNow,
                LastUpdatedByUserId: null);

            var service = new TextToSpeechService(
                providers: new ITextToSpeechProvider[] { legacyProvider, overlayProvider },
                credentialResolver: new FakeCredentialResolver(),
                settingsService: new FakeLegacySettings(legacySettings),
                tenantProvider: new FixedTenantProvider { TenantId = TenantId },
                currentUserProvider: new FixedUserProvider { UserId = UserId },
                aiRunWriter: new NoOpAiRunWriter(),
                rateLimiter: new AlwaysOpenRateLimiter(),
                ttsCache: new NoOpTtsCache(),
                logger: NullLogger<TextToSpeechService>.Instance,
                chatSpeechSettings: new FakeChatSpeechSettings(chat),
                speechProviderLibrary: new FakeSpeechProviderLibrary(chatSpeechProviderResolved));

            return Task.FromResult(new Fixture
            {
                Service = service,
                LegacyProvider = legacyProvider,
                OverlayProvider = overlayProvider,
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

    private sealed class FakeLegacySettings(TextToSpeechSettings settings) : ITenantTextToSpeechSettingsService
    {
        public Task<TextToSpeechSettings> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task<TextToSpeechSettings> SaveCurrentAsync(TextToSpeechSettings updated, CancellationToken cancellationToken = default) => Task.FromResult(updated);
    }

    private sealed class FakeChatSpeechSettings(ChatSpeechSettings settings) : IChatSpeechSettingsService
    {
        public Task<ChatSpeechSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task<ChatSpeechSettings> UpdateAsync(UpdateChatSpeechSettingsRequest request, CancellationToken cancellationToken = default) => Task.FromResult(settings);
    }

    private sealed class FakeSpeechProviderLibrary(SpeechProvider? resolved) : ISpeechProviderLibraryService
    {
        public Task<IReadOnlyList<SpeechProvider>> ListAsync(SpeechProviderType? type = null, bool includeDisabled = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SpeechProvider>>(resolved is null ? Array.Empty<SpeechProvider>() : new[] { resolved });
        public Task<SpeechProvider?> GetAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(resolved);
        public Task<SpeechProvider> CreateAsync(CreateSpeechProviderRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SpeechProvider> UpdateAsync(Guid id, UpdateSpeechProviderRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SpeechProvider> CloneBuiltInAsync(string builtInId, string? newDisplayName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SpeechProvider> SetStatusAsync(Guid id, SpeechProviderStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SpeechProviderHistoryEntry>> GetHistoryAsync(string id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SpeechProviderUsage> GetUsageAsync(string id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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

    private sealed class NoOpAiRunWriter : IAiRunWriter
    {
        public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkRunCompletedWithMetricsAsync(Guid aiRunId, int tokensUsed, int latencyMs, decimal costEstimate, string? outputRef = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
    }

    private sealed class NoOpTtsCache : ITtsCache
    {
        public bool IsAllowlisted(string normalizedText) => false;
        public ValueTask<TtsCacheEntry?> TryGetAsync(TtsCacheKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult<TtsCacheEntry?>(null);
        public ValueTask SetAsync(TtsCacheKey key, TtsCacheEntry entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FakeProvider(string name) : ITextToSpeechProvider
    {
        public string Name { get; } = name;
        public int SynthesizeCalls { get; private set; }
        public TextToSpeechProviderRequest? LastRequest { get; private set; }

        public Task<TextToSpeechProviderStreamResult> SynthesizeAsync(TextToSpeechProviderRequest request, CancellationToken cancellationToken = default)
        {
            SynthesizeCalls++;
            LastRequest = request;
            return Task.FromResult(new TextToSpeechProviderStreamResult(
                AudioStream: new MemoryStream(new byte[] { 1, 2, 3, 4 }, writable: false),
                ContentType: "audio/mpeg",
                Provider: Name,
                VoiceId: request.VoiceId,
                ModelId: request.ModelId));
        }

        public Task<IReadOnlyList<TextToSpeechVoiceOption>> GetVoicesAsync(string? apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TextToSpeechVoiceOption>>(Array.Empty<TextToSpeechVoiceOption>());
    }
}
