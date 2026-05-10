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
/// Service-level coverage of the speech provider library: validation, version bumping,
/// history snapshots, list/filter behaviour, status transitions, one-row-per-vendor enforcement,
/// and encrypted API-key handling. Uses EF Core InMemory so the tenant query filter machinery
/// on <c>AonikDbContextBase</c> can run end-to-end. The data protector is the ephemeral one
/// from <see cref="EphemeralDataProtectionProvider"/> so the encryption round-trip is real.
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
            _clock,
            new EphemeralDataProtectionProvider(),
            new NullSpeechCredentialCacheInvalidator());
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
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1")));

        p.IsBuiltIn.Should().BeFalse();
        p.Version.Should().Be(1);
        p.Status.Should().Be(SpeechProviderStatus.Active);
        p.DisplayName.Should().Be("My OpenAI TTS");
        p.Config.Should().BeOfType<OpenAITtsConfig>();
        p.HasApiKey.Should().BeFalse("no ApiKey was supplied");
    }

    [Fact]
    public async Task CreateAsync_Persists_Encrypted_Api_Key_And_Reports_HasApiKey_True()
    {
        var p = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "ElevenLabs",
            Type: SpeechProviderType.Tts,
            Vendor: "elevenlabs",
            Config: new ElevenLabsTtsConfig(
                DefaultModelId: "eleven_multilingual_v2",
                DefaultStability: null,
                DefaultSimilarityBoost: null,
                DefaultOptimizeStreamingLatency: null),
            ApiKey: "sk-test-elevenlabs"));

        p.HasApiKey.Should().BeTrue();
        // Raw key never returned via DTO — defence in depth.
        var raw = await _db.SpeechProviders.AsNoTracking()
            .Where(x => x.Vendor == "elevenlabs")
            .Select(x => x.EncryptedApiKey)
            .FirstAsync();
        raw.Should().NotBe("sk-test-elevenlabs", "the row stores the protected blob, not plaintext");
        raw.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_Rejects_Mismatch_Between_Type_And_Config()
    {
        var act = async () => await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "Wrong shape",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAIWhisperConfig(DefaultModel: "whisper-1", DefaultLanguage: null)));

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
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1")));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .WithMessage("*Display name is required*");
    }

    [Fact]
    public async Task CreateAsync_Rejects_Duplicate_Vendor_Type_Pair_For_Same_Tenant()
    {
        await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "First Mistral",
            Type: SpeechProviderType.Tts,
            Vendor: "mistral",
            Config: new MistralTtsConfig(DefaultModelId: "voxtral-tts")));

        var act = async () => await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "Second Mistral",
            Type: SpeechProviderType.Tts,
            Vendor: "mistral",
            Config: new MistralTtsConfig(DefaultModelId: "voxtral-tts")));

        await act.Should().ThrowAsync<SpeechLibraryValidationException>()
            .Where(ex => ex.Message.Contains("already exists") && ex.Message.Contains("mistral"));
    }

    [Fact]
    public async Task CreateAsync_Allows_Same_Vendor_With_Different_Types()
    {
        // OpenAI offers both Whisper STT and TTS — admins should be able to set up both.
        await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "OpenAI Whisper",
            Type: SpeechProviderType.Stt,
            Vendor: "openai",
            Config: new OpenAIWhisperConfig(DefaultModel: "whisper-1", DefaultLanguage: null)));

        var tts = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "OpenAI TTS",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1")));

        tts.Vendor.Should().Be("openai");
        tts.Type.Should().Be(SpeechProviderType.Tts);

        var all = await _sut.ListAsync();
        all.Should().HaveCount(2);
    }

    // ── Update + history ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Bumps_Version_And_Appends_History_Snapshot()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "v1 name",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1")));

        _clock.Advance(TimeSpan.FromMinutes(5));

        var guid = Guid.Parse(created.Id);
        var updated = await _sut.UpdateAsync(guid, new UpdateSpeechProviderRequest(
            DisplayName: "v2 name",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1-hd")));

        updated.Version.Should().Be(2);
        updated.DisplayName.Should().Be("v2 name");
        ((OpenAITtsConfig)updated.Config).DefaultModelId.Should().Be("tts-1-hd");

        var history = await _sut.GetHistoryAsync(updated.Id);
        history.Should().HaveCount(1);
        history[0].Version.Should().Be(1);
        history[0].Action.Should().Be(SpeechProviderHistoryAction.Updated);
        history[0].SnapshotDisplayName.Should().Be("v1 name");
        ((OpenAITtsConfig)history[0].SnapshotConfig).DefaultModelId.Should().Be("tts-1");
    }

    [Fact]
    public async Task UpdateAsync_Tri_State_ApiKey_Null_Leaves_Existing()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "OpenAI",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1"),
            ApiKey: "sk-original"));
        var guid = Guid.Parse(created.Id);

        await _sut.UpdateAsync(guid, new UpdateSpeechProviderRequest(
            DisplayName: "renamed",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1"),
            ApiKey: null));   // null == leave alone

        var refreshed = await _sut.GetAsync(created.Id);
        refreshed!.HasApiKey.Should().BeTrue("null ApiKey should not clear the existing credential");
    }

    [Fact]
    public async Task UpdateAsync_Tri_State_ApiKey_Empty_String_Clears_Stored()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "OpenAI",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1"),
            ApiKey: "sk-original"));
        var guid = Guid.Parse(created.Id);

        await _sut.UpdateAsync(guid, new UpdateSpeechProviderRequest(
            DisplayName: created.DisplayName,
            Config: created.Config,
            ApiKey: ""));      // explicit clear

        var refreshed = await _sut.GetAsync(created.Id);
        refreshed!.HasApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_History_Is_Newest_First_And_Capped()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: "name v1",
            Type: SpeechProviderType.Tts,
            Vendor: "openai",
            Config: new OpenAITtsConfig(DefaultModelId: "tts-1")));

        var guid = Guid.Parse(created.Id);
        for (var i = 2; i <= SpeechLibraryConstants.HistoryRetentionPerEntity + 5; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await _sut.UpdateAsync(guid, new UpdateSpeechProviderRequest(
                DisplayName: $"name v{i}",
                Config: new OpenAITtsConfig(DefaultModelId: $"tts-1-rev{i}")));
        }

        var history = await _sut.GetHistoryAsync(created.Id);
        history.Should().HaveCount(SpeechLibraryConstants.HistoryRetentionPerEntity);
        history[0].Version.Should().Be(SpeechLibraryConstants.HistoryRetentionPerEntity + 4);
    }

    // ── List ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_Returns_Empty_When_Tenant_Has_No_Rows()
    {
        var all = await _sut.ListAsync();
        all.Should().BeEmpty("the built-in catalog ships empty — tenants build their own library");
    }

    [Fact]
    public async Task ListAsync_Filters_By_Type()
    {
        await _sut.CreateAsync(new CreateSpeechProviderRequest(
            "tenant-stt",
            SpeechProviderType.Stt,
            "openai-whisper",
            new OpenAIWhisperConfig(DefaultModel: "whisper-1", DefaultLanguage: "en")));
        await _sut.CreateAsync(new CreateSpeechProviderRequest(
            "tenant-tts",
            SpeechProviderType.Tts,
            "openai",
            new OpenAITtsConfig(DefaultModelId: "tts-1")));

        var sttOnly = await _sut.ListAsync(type: SpeechProviderType.Stt);

        sttOnly.Should().OnlyContain(p => p.Type == SpeechProviderType.Stt);
        sttOnly.Should().Contain(p => p.DisplayName == "tenant-stt");
        sttOnly.Should().NotContain(p => p.DisplayName == "tenant-tts");
    }

    [Fact]
    public async Task ListAsync_Excludes_Disabled_By_Default()
    {
        var created = await _sut.CreateAsync(new CreateSpeechProviderRequest(
            "to-disable",
            SpeechProviderType.Tts,
            "openai",
            new OpenAITtsConfig(DefaultModelId: "tts-1")));

        await _sut.SetStatusAsync(Guid.Parse(created.Id), SpeechProviderStatus.Disabled);

        var defaultList = await _sut.ListAsync();
        defaultList.Should().NotContain(p => p.Id == created.Id);

        var withDisabled = await _sut.ListAsync(includeDisabled: true);
        withDisabled.Should().Contain(p => p.Id == created.Id);
    }

    // ── Clone ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloneBuiltInAsync_Always_Throws_Now_That_Catalog_Is_Empty()
    {
        var act = async () => await _sut.CloneBuiltInAsync("built-in:openai-tts-alloy", "anything");
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
            new OpenAITtsConfig(DefaultModelId: "tts-1")));

        await _sut.SetStatusAsync(Guid.Parse(created.Id), SpeechProviderStatus.SoftDeleted);

        (await _sut.GetAsync(created.Id)).Should().BeNull();
        (await _sut.ListAsync()).Should().NotContain(p => p.Id == created.Id);
    }

    // ── Get ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Returns_Null_For_Built_In_Reserved_Id_Now_That_Catalog_Is_Empty()
    {
        var p = await _sut.GetAsync("built-in:openai-tts-alloy");
        p.Should().BeNull();
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
