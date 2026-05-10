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
/// (referenced ids must resolve to the right type), version bumping, history, built-in merge.
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
    public async Task ListAsync_Returns_Built_Ins_Even_When_Tenant_Has_No_Rows()
    {
        var all = await _sut.ListAsync();
        all.Should().NotBeEmpty();
        all.Should().OnlyContain(r => r.IsBuiltIn);
    }

    [Fact]
    public async Task CreateAsync_Persists_A_Chained_Recipe_That_References_Built_In_Providers()
    {
        var r = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "My recipe",
            Description: "Test",
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: "built-in:openai-whisper-default",
                TtsProviderId: "built-in:openai-tts-alloy",
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: 800,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: null));

        r.IsBuiltIn.Should().BeFalse();
        r.Kind.Should().Be(VoiceRecipeKind.Chained);
        r.Chained!.SttProviderId.Should().Be("built-in:openai-whisper-default");
        r.Chained.TtsProviderId.Should().Be("built-in:openai-tts-alloy");
        r.Composite.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Rejects_Chained_Recipe_That_References_A_Tts_Provider_For_Stt_Slot()
    {
        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Wrong slot",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                // alloy is a TTS provider; using it as the STT id should be rejected.
                SttProviderId: "built-in:openai-tts-alloy",
                TtsProviderId: "built-in:openai-tts-alloy",
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
        var act = async () => await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Bad shape",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                SttProviderId: "built-in:openai-whisper-default",
                TtsProviderId: "built-in:openai-tts-alloy",
                PinnedAgentId: null,
                Vad: "energy",
                VadStopMs: null,
                TranscriptionFilter: true,
                SentenceAggregator: true),
            Composite: new CompositeRecipeBody("built-in:openai-realtime", null)));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*Chained recipes must NOT include a composite body*");
    }

    [Fact]
    public async Task CreateAsync_Persists_A_Composite_Recipe()
    {
        var r = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "Realtime",
            Description: "OpenAI Realtime",
            Kind: VoiceRecipeKind.Composite,
            Chained: null,
            Composite: new CompositeRecipeBody(
                CompositeProviderId: "built-in:openai-realtime",
                PinnedAgentId: null)));

        r.Kind.Should().Be(VoiceRecipeKind.Composite);
        r.Composite!.CompositeProviderId.Should().Be("built-in:openai-realtime");
        r.Chained.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Bumps_Version_And_Records_History()
    {
        var created = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: "v1",
            Description: null,
            Kind: VoiceRecipeKind.Chained,
            Chained: new ChainedRecipeBody(
                "built-in:openai-whisper-default",
                "built-in:openai-tts-alloy",
                null, "energy", 800, true, true),
            Composite: null));

        _clock.Advance(TimeSpan.FromMinutes(1));

        var updated = await _sut.UpdateAsync(Guid.Parse(created.Id), new UpdateVoiceRecipeRequest(
            DisplayName: "v2",
            Description: "now with description",
            Chained: new ChainedRecipeBody(
                "built-in:openai-whisper-default",
                "built-in:openai-tts-onyx-hd", // changed
                null, "silero", 1200, true, true),
            Composite: null));

        updated.Version.Should().Be(2);
        updated.Chained!.TtsProviderId.Should().Be("built-in:openai-tts-onyx-hd");
        updated.Chained.Vad.Should().Be("silero");

        var history = await _sut.GetHistoryAsync(updated.Id);
        history.Should().HaveCount(1);
        history[0].SnapshotChained!.TtsProviderId.Should().Be("built-in:openai-tts-alloy");
    }

    [Fact]
    public async Task CloneBuiltInAsync_Creates_Tenant_Row_From_Built_In_Recipe()
    {
        var clone = await _sut.CloneBuiltInAsync("built-in:cost-chained-openai", "My cost recipe");

        clone.IsBuiltIn.Should().BeFalse();
        clone.DisplayName.Should().Be("My cost recipe");
        clone.Kind.Should().Be(VoiceRecipeKind.Chained);
        clone.Chained!.SttProviderId.Should().Be("built-in:openai-whisper-default");
    }

    [Fact]
    public async Task ProviderUsage_Now_Surfaces_Recipe_References()
    {
        // Author a recipe referencing the OpenAI Whisper built-in.
        var recipe = await _sut.CreateAsync(new CreateVoiceRecipeRequest(
            "uses-whisper", null,
            VoiceRecipeKind.Chained,
            new ChainedRecipeBody("built-in:openai-whisper-default", "built-in:openai-tts-alloy", null, "energy", 800, true, true),
            null));

        var usage = await _providers.GetUsageAsync("built-in:openai-whisper-default");

        // Should include both the tenant's recipe AND the four built-in recipes that use Whisper.
        usage.RecipesUsingThisProvider.Should().Contain(r => r.RecipeId == recipe.Id);
        usage.RecipesUsingThisProvider.Should().Contain(r => r.RecipeId == "built-in:cost-chained-openai");
    }

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
