using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Library;
using Aonik.Voice.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Tests.Library;

/// <summary>
/// Service-level coverage of the recipe library: validation against the provider library
/// (referenced ids must resolve to the right type), version bumping, history, status
/// transitions. Every test seeds tenant-owned providers first because the built-in catalog
/// was emptied when the library moved to a "create-your-own" flow.
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
        _providers = new SpeechProviderLibraryService(_db, builtIns, tenant, user, _clock);
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
        var tts = await SeedTtsProvider("Alloy");

        var r = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "My recipe",
            Description: "Test",
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
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
        r.Composite.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Rejects_Chained_Recipe_That_References_A_Tts_Provider_For_Stt_Slot()
    {
        var tts = await SeedTtsProvider("Alloy");

        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Wrong slot",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                // Using a TTS provider as the STT id should be rejected.
                SttProviderId: tts,
                TtsProviderId: tts,
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
        var tts = await SeedTtsProvider("Alloy");
        var composite = await SeedCompositeProvider("Realtime");

        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Bad shape",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: stt,
                TtsProviderId: tts,
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: null,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: new CompositeRecipeBody(composite, null)));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*Chained recipes must NOT include a composite body*");
    }

    [Fact]
    public async Task CreateAsync_Persists_A_Composite_Recipe()
    {
        var composite = await SeedCompositeProvider("Realtime");

        var r = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Realtime",
            Description: "OpenAI Realtime",
            Kind: VoiceRecipeKind.Composite,
            Chained: null,
            Composite: new CompositeRecipeBody(
                CompositeProviderId: composite,
                PinnedAgentId: null)));

        r.Kind.Should().Be(VoiceRecipeKind.Composite);
        r.Composite!.CompositeProviderId.Should().Be(composite);
        r.Chained.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Bumps_Version_And_Records_History()
    {
        var stt = await SeedSttProvider("Whisper");
        var ttsAlloy = await SeedTtsProvider("Alloy");
        var ttsOnyx = await SeedTtsProvider("Onyx");

        var created = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "v1",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(stt, ttsAlloy, null, "energy", 800, true, true),
            Composite: null));

        _clock.Advance(TimeSpan.FromMinutes(1));

        var updated = await _sut.UpdateAsync(Guid.Parse(created.Id), new UpdateVoiceRecipeRequest(
            DisplayName: "v2",
            Description: "now with description",
            Chained: new ChainedRecipeBody(stt, ttsOnyx, null, "silero", 1200, true, true),
            Composite: null));

        updated.Version.Should().Be(2);
        updated.Chained!.TtsProviderId.Should().Be(ttsOnyx);
        updated.Chained.Vad.Should().Be("silero");

        var history = await _sut.GetHistoryAsync(updated.Id);
        history.Should().HaveCount(1);
        history[0].SnapshotChained!.TtsProviderId.Should().Be(ttsAlloy);
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
        var tts = await SeedTtsProvider("Alloy");

        var recipe = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            "uses-whisper", null,
            VoiceRecipeKind.Chained,
            new ChainedRecipeBody(stt, tts, null, "energy", 800, true, true),
            null));

        var usage = await _providers.GetUsageAsync(stt);

        usage.RecipesUsingThisProvider.Should().Contain(r => r.RecipeId == recipe.Id);
    }

    // ── Seeders ────────────────────────────────────────────────────────────

    private async Task<string> SeedSttProvider(string name) =>
        (await _providers.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: SpeechProviderType.Stt,
            Vendor: "openai",
            Config: new OpenAIWhisperConfig(Model: "whisper-1", Language: null)))).Id;

    private async Task<string> SeedTtsProvider(string name) =>
        (await _providers.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(VoiceId: name.ToLowerInvariant(), ModelId: "tts-1")))).Id;

    private async Task<string> SeedCompositeProvider(string name) =>
        (await _providers.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: SpeechProviderType.Composite,
            Vendor: "openai-realtime",
            Config: new OpenAIRealtimeCompositeConfig(
                Voice: "alloy",
                Model: "gpt-realtime-mini",
                InstructionsAddendum: null)))).Id;

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
