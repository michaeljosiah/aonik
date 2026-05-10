using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Library;
using Aonik.Voice.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Tests.Library;

/// <summary>
/// Service-level coverage of the recipe library: validation against the provider library
/// (referenced ids must resolve to the right type), the Phase D requirement that voice id is
/// pinned on the recipe (not the provider), version bumping, history, status transitions.
/// Every test seeds tenant-owned providers first because the built-in catalog was emptied when
/// the library moved to a "create-your-own" flow.
/// </summary>
public class VoiceRecipeLibraryServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly TestClock _clock = new();
    private readonly VoiceDbContext _db;
    private readonly SpeechProviderLibraryService _providers;
    private readonly VoiceRecipeLibraryService _sut;

    public VoiceRecipeLibraryServiceTests()
    {
        var opts = new DbContextOptionsBuilder<VoiceDbContext>()
            .UseInMemoryDatabase($"VoiceRecipeTests_{Guid.NewGuid()}")
            .Options;

        var tenant = new TestTenantProvider(_tenantId);
        var user = new TestCurrentUserProvider(_userId);
        var builtIns = new BuiltInSpeechCatalog();

        _db = new VoiceDbContext(opts, tenant, user, _clock);
        _providers = new SpeechProviderLibraryService(
            _db, builtIns, tenant, user, _clock,
            new EphemeralDataProtectionProvider(),
            new NullSpeechCredentialCacheInvalidator());
        _sut = new VoiceRecipeLibraryService(_db, builtIns, _providers, tenant, user, _clock);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ListAsync_Returns_Empty_When_Tenant_Has_No_Recipes()
    {
        var all = await _sut.ListAsync();
        all.Should().BeEmpty("the built-in catalog ships empty — recipes are tenant-authored");
    }

    [Fact]
    public async Task CreateAsync_Persists_A_Chained_Recipe_That_References_Tenant_Providers()
    {
        var stt = await SeedSttProvider("Whisper");
        var tts = await SeedTtsProvider("OpenAI TTS");

        var r = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "My recipe",
            Description: "Test",
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                TtsVoiceId: "alloy",
                TtsModelId: "tts-1",
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: 800,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: null));

        r.IsBuiltIn.Should().BeFalse();
        r.Kind.Should().Be(VoiceRecipeKind.Chained);
        r.Chained!.SttProviderId.Should().Be(stt);
        r.Chained.TtsProviderId.Should().Be(tts);
        r.Chained.TtsVoiceId.Should().Be("alloy");
        r.Composite.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Rejects_Chained_Recipe_Without_TtsVoiceId()
    {
        var stt = await SeedSttProvider("Whisper");
        var tts = await SeedTtsProvider("OpenAI TTS");

        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "no voice",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                TtsVoiceId: "",
                TtsModelId: null,
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: null,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: null));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*TTS voice id*");
    }

    [Fact]
    public async Task CreateAsync_Rejects_Chained_Recipe_That_References_A_Tts_Provider_For_Stt_Slot()
    {
        var tts = await SeedTtsProvider("OpenAI TTS");

        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Wrong slot",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: tts,
                TtsProviderId: tts,
                TtsVoiceId: "alloy",
                TtsModelId: null,
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: null,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: null));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*type Tts, expected Stt*");
    }

    [Fact]
    public async Task CreateAsync_Rejects_Chained_Recipe_With_Composite_Body()
    {
        var stt = await SeedSttProvider("Whisper");
        var tts = await SeedTtsProvider("OpenAI TTS");
        var composite = await SeedCompositeProvider("Realtime");

        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Bad shape",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                TtsVoiceId: "alloy",
                TtsModelId: null,
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: null,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: new CompositeRecipeBody(composite, "alloy", null, null, null)));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*Chained recipes must NOT include a composite body*");
    }

    [Fact]
    public async Task CreateAsync_Persists_A_Composite_Recipe_With_Voice()
    {
        var composite = await SeedCompositeProvider("Realtime");

        var r = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Realtime",
            Description: "OpenAI Realtime",
            Kind: VoiceRecipeKind.Composite,
            Chained: null,
            Composite: new CompositeRecipeBody(
                CompositeProviderId: composite,
                Voice: "shimmer",
                Model: "gpt-realtime",
                InstructionsAddendum: "Be concise.",
                PinnedAgentId: null)));

        r.Kind.Should().Be(VoiceRecipeKind.Composite);
        r.Composite!.CompositeProviderId.Should().Be(composite);
        r.Composite.Voice.Should().Be("shimmer");
        r.Chained.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Rejects_Composite_Recipe_Without_Voice()
    {
        var composite = await SeedCompositeProvider("Realtime");

        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "No voice",
            Description: null,
            Kind: VoiceRecipeKind.Composite,
            Chained: null,
            Composite: new CompositeRecipeBody(
                CompositeProviderId: composite,
                Voice: "",
                Model: null,
                InstructionsAddendum: null,
                PinnedAgentId: null)));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*specify a voice*");
    }

    [Fact]
    public async Task UpdateAsync_Bumps_Version_And_Records_History()
    {
        var stt = await SeedSttProvider("Whisper");
        var tts = await SeedTtsProvider("OpenAI TTS");

        var created = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "v1",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                TtsVoiceId: "alloy",
                TtsModelId: "tts-1",
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: 800,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: null));

        _clock.Advance(TimeSpan.FromMinutes(1));

        var updated = await _sut.UpdateAsync(Guid.Parse(created.Id), new UpdateVoiceRecipeRequest(
            DisplayName: "v2",
            Description: "now with description",
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                TtsVoiceId: "onyx",
                TtsModelId: "tts-1-hd",
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "silero",
                VadStopMs: 1200,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: null));

        updated.Version.Should().Be(2);
        updated.Chained!.TtsVoiceId.Should().Be("onyx");
        updated.Chained.Vad.Should().Be("silero");

        var history = await _sut.GetHistoryAsync(updated.Id);
        history.Should().HaveCount(1);
        history[0].SnapshotChained!.TtsVoiceId.Should().Be("alloy");
    }

    [Fact]
    public async Task CloneBuiltInAsync_Always_Throws_Now_That_Catalog_Is_Empty()
    {
        var act = async () => await _sut.CloneBuiltInAsync("built-in:cost-chained-openai", "anything");
        await act.Should().ThrowAsync<SpeechLibraryValidationException>();
    }

    [Fact]
    public async Task ProviderUsage_Surfaces_Tenant_Recipe_References()
    {
        var stt = await SeedSttProvider("Whisper");
        var tts = await SeedTtsProvider("OpenAI TTS");

        var recipe = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            "uses-whisper", null,
            VoiceRecipeKind.Chained,
            new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                TtsVoiceId: "alloy",
                TtsModelId: null,
                SttModel: null,
                SttLanguage: null,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: 800,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            null));

        var usage = await _providers.GetUsageAsync(stt);

        usage.RecipesUsingThisProvider.Should().Contain(r => r.RecipeId == recipe.Id);
    }

    // ── Seeders ────────────────────────────────────────────────────────────

    private async Task<string> SeedSttProvider(string name) =>
        (await _providers.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: SpeechProviderType.Stt,
            Vendor: "openai-whisper",
            Config: new OpenAIWhisperConfig(DefaultModel: "whisper-1", DefaultLanguage: null)))).Id;

    private async Task<string> SeedTtsProvider(string name) =>
        (await _providers.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1")))).Id;

    private async Task<string> SeedCompositeProvider(string name) =>
        (await _providers.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: SpeechProviderType.Composite,
            Vendor: "openai-realtime",
            Config: new OpenAIRealtimeCompositeConfig(
                DefaultModel: "gpt-realtime-mini",
                DefaultInstructionsAddendum: null)))).Id;

    // ── Test plumbing ──────────────────────────────────────────────────────

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _id;
        public TestTenantProvider(Guid id) => _id = id;
        public Guid GetCurrentTenantId() => _id;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _id; return true; }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _id;
        public TestCurrentUserProvider(Guid id) => _id = id;
        public Guid? GetCurrentUserId() => _id;
        public bool TryGetCurrentUserId(out Guid userId) { userId = _id; return true; }
    }

    private sealed class TestClock : IClock
    {
        private DateTime _now = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
