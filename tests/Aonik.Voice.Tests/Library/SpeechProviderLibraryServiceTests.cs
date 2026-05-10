using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Library;
using Aonik.Voice.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Tests.Library;

/// <summary>
/// Service-level coverage of the speech provider library: validation, version bumping,
/// history snapshots, built-in merge into list responses, clone behavior. Uses EF Core
/// InMemory so the tenant query filter machinery on <c>AonikDbContextBase</c> can run end-to-end.
/// </summary>
public class SpeechProviderLibraryServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly TestClock _clock = new();
    private readonly VoiceDbContext _db;
    private readonly SpeechProviderLibraryService _sut;

    public SpeechProviderLibraryServiceTests()
    {
        var opts = new DbContextOptionsBuilder<VoiceDbContext>()
            .UseInMemoryDatabase($"VoiceLibraryTests_{Guid.NewGuid()}")
            .Options;

        _db = new VoiceDbContext(
            opts,
            new TestTenantProvider(_tenantId),
            new TestCurrentUserProvider(_userId),
            _clock);

        _sut = new SpeechProviderLibraryService(
            _db,
            new BuiltInSpeechCatalog(),
            new TestTenantProvider(_tenantId),
            new TestCurrentUserProvider(_userId),
            _clock);
    }

    public void Dispose() => _db.Dispose();

    // ── Create + validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Persists_Tenant_Owned_Provider_With_Version_One()
    {
        var p = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "My OpenAI TTS",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")));

        p.IsBuiltIn.Should().BeFalse();
        p.Version.Should().Be(1);
        p.Status.Should().Be(SpeechProviderStatus.Active);
        p.DisplayName.Should().Be("My OpenAI TTS");
        p.Config.Should().BeOfType<OpenAITtsConfig>();
    }

    [Fact]
    public async Task CreateAsync_Rejects_Mismatch_Between_Type_And_Config()
    {
        // Trying to create a TTS provider with an STT-shaped config.
        var act = async () => await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "Wrong shape",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAIWhisperConfig(Model: "whisper-1", Language: null)));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*Type=Tts*Vendor=openai*");
    }

    [Fact]
    public async Task CreateAsync_Rejects_Empty_Display_Name()
    {
        var act = async () => await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "   ",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*Display name is required*");
    }

    // ── Update + history ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Bumps_Version_And_Appends_History_Snapshot()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "v1 name",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")));

        _clock.Advance(TimeSpan.FromMinutes(5));

        var guid = Guid.Parse(created.Id);
        var updated = await _sut.UpdateAsync(guid, new UpdateSpeechProviderRequest(
            DisplayName: "v2 name",
            Config: new OpenAITtsConfig(VoiceId: "onyx", ModelId: "tts-1-hd")));

        updated.Version.Should().Be(2);
        updated.DisplayName.Should().Be("v2 name");
        ((OpenAITtsConfig)updated.Config).VoiceId.Should().Be("onyx");

        var history = await _sut.GetHistoryAsync(updated.Id);
        history.Should().HaveCount(1, "the v1 snapshot was archived when v2 saved");
        history[0].Version.Should().Be(1);
        history[0].Action.Should().Be(SpeechProviderHistoryAction.Updated);
        history[0].SnapshotDisplayName.Should().Be("v1 name");
        ((OpenAITtsConfig)history[0].SnapshotConfig).VoiceId.Should().Be("alloy");
    }

    [Fact]
    public async Task UpdateAsync_History_Is_Newest_First_And_Capped()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "name v1",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")));

        var guid = Guid.Parse(created.Id);
        for (var i = 2; i <= SpeechLibraryConstants.HistoryRetentionPerEntity + 5; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await _sut.UpdateAsync(guid, new UpdateSpeechProviderRequest(
                DisplayName: $"name v{i}",
                Config: new OpenAITtsConfig(VoiceId: "alloy", ModelId: $"tts-1-rev{i}")));
        }

        var history = await _sut.GetHistoryAsync(created.Id);
        history.Should().HaveCount(SpeechLibraryConstants.HistoryRetentionPerEntity);
        // After 29 updates the live row is at Version 30; the most recent archived snapshot is
        // of Version 29 (the version that existed *before* the 30th update applied).
        history[0].Version.Should().Be(SpeechLibraryConstants.HistoryRetentionPerEntity + 4,
            "ring buffer is newest-first; the most recent archived snapshot is index 0");
    }

    // ── List + built-in merge ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_Returns_Built_Ins_Even_When_Tenant_Has_No_Rows()
    {
        var all = await _sut.ListAsync();
        all.Should().NotBeEmpty();
        all.Should().OnlyContain(p => p.IsBuiltIn);
    }

    [Fact]
    public async Task ListAsync_Filters_By_Type()
    {
        await _sut.CreateAsync(new CreateSpeechProviderRequest(
            "tenant-stt",
            SpeechProviderType.Stt,
            "openai-whisper",
            new OpenAIWhisperConfig(Model: "whisper-1", Language: "en")));

        var sttOnly = await _sut.ListAsync(type: SpeechProviderType.Stt);

        sttOnly.Should().OnlyContain(p => p.Type == SpeechProviderType.Stt);
        sttOnly.Should().Contain(p => p.IsBuiltIn);     // built-in STTs included
        sttOnly.Should().Contain(p => !p.IsBuiltIn);    // tenant row included
    }

    [Fact]
    public async Task ListAsync_Excludes_Disabled_By_Default()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            "to-disable",
            SpeechProviderType.Tts,
            "openai",
            new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")));

        await _sut.SetStatusAsync(Guid.Parse(created.Id), SpeechProviderStatus.Disabled);

        var defaultList = await _sut.ListAsync();
        defaultList.Should().NotContain(p => p.Id == created.Id);

        var withDisabled = await _sut.ListAsync(includeDisabled: true);
        withDisabled.Should().Contain(p => p.Id == created.Id);
    }

    // ── Clone ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloneBuiltInAsync_Creates_Tenant_Row_With_Same_Config()
    {
        var clone = await _sut.CloneBuiltInAsync(
            "built-in:openai-tts-alloy",
            newDisplayName: "My alloy");

        clone.IsBuiltIn.Should().BeFalse();
        clone.DisplayName.Should().Be("My alloy");
        clone.Type.Should().Be(SpeechProviderType.Tts);
        clone.Vendor.Should().Be("openai");
        ((OpenAITtsConfig)clone.Config).VoiceId.Should().Be("alloy");
    }

    [Fact]
    public async Task CloneBuiltInAsync_Defaults_DisplayName_When_Not_Supplied()
    {
        var clone = await _sut.CloneBuiltInAsync("built-in:openai-tts-alloy", null);
        clone.DisplayName.Should().EndWith("(copy)");
    }

    [Fact]
    public async Task CloneBuiltInAsync_Throws_For_Unknown_Built_In()
    {
        var act = async () => await _sut.CloneBuiltInAsync("built-in:does-not-exist", null);
        await act.Should().ThrowAsync<SpeechLibraryValidationException>();
    }

    // ── Status transitions ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetStatusAsync_Soft_Delete_Hides_From_Default_List_And_Get()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            "doomed",
            SpeechProviderType.Tts,
            "openai",
            new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")));

        await _sut.SetStatusAsync(Guid.Parse(created.Id), SpeechProviderStatus.SoftDeleted);

        (await _sut.GetAsync(created.Id)).Should().BeNull();
        (await _sut.ListAsync()).Should().NotContain(p => p.Id == created.Id);
    }

    // ── Get ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Returns_Built_In_For_Reserved_Id()
    {
        var p = await _sut.GetAsync("built-in:openai-tts-alloy");
        p.Should().NotBeNull();
        p!.IsBuiltIn.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Returns_Null_For_Unknown_Guid()
    {
        var p = await _sut.GetAsync(Guid.NewGuid().ToString("N"));
        p.Should().BeNull();
    }

    // ── Test plumbing ──────────────────────────────────────────────────────────────────

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
