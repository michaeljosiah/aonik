using Aonik.Platform.Contracts.Models.ReferenceData;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Packs;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Packs;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 065 — proves the config-pack applier actually applies a pack to a tenant at provision time:
/// tenant-scoped settings (additive-only), reference data, agent overrides, and the recorded version.
/// </summary>
public sealed class ConfigPackApplierTests
{
    [Fact]
    public void Source_LoadsInstalledManifests_AndReturnsNullForUnknown()
    {
        var source = new ConfigPackSource();

        source.ListBusinessTypes().Should().Contain(new[] { "base", "food-commerce", "simi" });

        var pack = source.Get("food-commerce");
        pack.Should().NotBeNull();
        pack!.Modules.Should().Contain("Commerce");
        pack.ReferenceData.Should().ContainSingle().Which.Items.Should().HaveCount(5);

        source.Get("nonexistent-type").Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_AppliesFoodCommercePack_SettingsReferenceDataAndVersion()
    {
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext(tenantId, out _);
        SeedTenant(db, tenantId, "food-commerce");
        await db.SaveChangesAsync();

        var referenceData = new RecordingReferenceDataService();
        var agents = new RecordingAgentConfigurationService();
        var applier = new ConfigPackApplier(new ConfigPackSource(), db, agents, referenceData, new FakeTenantContext());

        var result = await applier.ApplyAsync(tenantId, "food-commerce");

        result.AppliedVersion.Should().Be(1);

        var settings = await db.Settings
            .Where(s => s.TenantId == tenantId && s.Scope == SettingScope.Tenant)
            .ToListAsync();
        settings.Select(s => s.Key).Should().BeEquivalentTo("Commerce.Enabled", "Branding.AgentDisplayName");

        referenceData.Upserts.Should().HaveCount(5);
        referenceData.Upserts.Should().OnlyContain(u => u.Type == "unit_of_measure" && u.TenantId == tenantId);

        agents.Upserts.Should().BeEmpty(); // food-commerce carries no agent overrides

        var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantId);
        tenant.AppliedPackVersion.Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_AppliesSimiAgentOverride()
    {
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext(tenantId, out _);
        SeedTenant(db, tenantId, "simi");
        await db.SaveChangesAsync();

        var agents = new RecordingAgentConfigurationService();
        var applier = new ConfigPackApplier(new ConfigPackSource(), db, agents, new RecordingReferenceDataService(), new FakeTenantContext());

        var result = await applier.ApplyAsync(tenantId, "simi");

        result.AppliedVersion.Should().Be(1);
        agents.Upserts.Should().ContainSingle().Which.Should().Be("personal-finance-agent");
        (await db.Settings.CountAsync(s => s.TenantId == tenantId && s.Scope == SettingScope.Tenant)).Should().Be(2);
    }

    [Fact]
    public async Task ApplyAsync_IsAdditiveOnly_DoesNotOverwriteExistingSetting()
    {
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext(tenantId, out _);
        SeedTenant(db, tenantId, "food-commerce");
        db.Settings.Add(new global::Aonik.Platform.Entities.Settings.Setting
        {
            Key = "Commerce.Enabled",
            Value = "false", // an existing (admin-edited) value
            Scope = SettingScope.Tenant,
            TenantId = tenantId,
        });
        await db.SaveChangesAsync();

        var applier = new ConfigPackApplier(new ConfigPackSource(), db, new RecordingAgentConfigurationService(), new RecordingReferenceDataService(), new FakeTenantContext());

        await applier.ApplyAsync(tenantId, "food-commerce");

        // The pack sets Commerce.Enabled=true, but the pre-existing value must survive (never overwrite).
        var existing = await db.Settings.SingleAsync(s => s.TenantId == tenantId && s.Key == "Commerce.Enabled");
        existing.Value.Should().Be("false");
    }

    [Fact]
    public async Task ApplyAsync_ForBaseTenant_AppliesNoConfiguration()
    {
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext(tenantId, out _);
        SeedTenant(db, tenantId, "base");
        await db.SaveChangesAsync();

        var applier = new ConfigPackApplier(new ConfigPackSource(), db, new RecordingAgentConfigurationService(), new RecordingReferenceDataService(), new FakeTenantContext());

        await applier.ApplyAsync(tenantId, "base");

        (await db.Settings.CountAsync(s => s.TenantId == tenantId && s.Scope == SettingScope.Tenant)).Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_IsAdditiveOnly_OnReapply_DoesNotDuplicateSettingsOrReferenceData()
    {
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext(tenantId, out _);
        SeedTenant(db, tenantId, "food-commerce");
        await db.SaveChangesAsync();

        var referenceData = new RecordingReferenceDataService();
        var applier = new ConfigPackApplier(new ConfigPackSource(), db, new RecordingAgentConfigurationService(), referenceData, new FakeTenantContext());

        await applier.ApplyAsync(tenantId, "food-commerce"); // first apply
        await applier.ApplyAsync(tenantId, "food-commerce"); // re-apply must not clobber or duplicate

        referenceData.Upserts.Should().HaveCount(5); // inserted once; skipped on re-apply (Codex review)
        (await db.Settings.CountAsync(s => s.TenantId == tenantId && s.Scope == SettingScope.Tenant)).Should().Be(2);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static PlatformDbContext NewDbContext(Guid tenantId, out Guid _tenantId)
    {
        _tenantId = tenantId;
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"ConfigPacks_{Guid.NewGuid()}")
            .Options;
        return new PlatformDbContext(options, new TestTenantProvider(tenantId));
    }

    private static void SeedTenant(PlatformDbContext db, Guid tenantId, string businessType)
        => db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant-{tenantId:N}",
            Environment = "Test",
            DefaultCurrency = "GBP",
            BusinessType = businessType,
            SupportedCountriesJson = "[]",
            AllowedOriginCountriesJson = "[]",
            AllowedDestinationCountriesJson = "[]",
        });

    private sealed class RecordingReferenceDataService : IReferenceDataService
    {
        private readonly List<ReferenceDataItemSnapshot> _store = new();
        public List<(string Type, string Code, Guid? TenantId)> Upserts { get; } = new();

        // Stateful so a re-apply sees prior items as "existing" and the additive-only path skips them.
        public Task<IReadOnlyList<ReferenceDataItemSnapshot>> GetAsync(string type, Guid? tenantId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReferenceDataItemSnapshot>>(_store.Where(s => s.Type == type).ToList());

        public Task<ReferenceDataItemSnapshot> UpsertAsync(ReferenceDataItemUpsert request, Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            Upserts.Add((request.Type, request.Code, tenantId));
            var snapshot = new ReferenceDataItemSnapshot(request.Type, request.Code, request.DisplayName, request.SortOrder, request.IsActive);
            _store.Add(snapshot);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class RecordingAgentConfigurationService : IAgentConfigurationService
    {
        public List<string> Upserts { get; } = new();

        public Task<AgentConfigurationResponse> UpsertOverrideAsync(string agentName, UpsertAgentConfigurationRequest request, CancellationToken cancellationToken = default)
        {
            Upserts.Add(agentName);
            return Task.FromResult<AgentConfigurationResponse>(null!); // the applier discards the return
        }

        public Task<IReadOnlyList<AgentConfigurationResponse>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentConfigurationResponse>>(Array.Empty<AgentConfigurationResponse>());

        public Task<AgentConfigurationResponse?> GetResolvedAsync(string agentName, CancellationToken cancellationToken = default)
            => Task.FromResult<AgentConfigurationResponse?>(null);

        public Task DeleteOverrideAsync(string agentName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AgentConfigurationResponse> ResetPromptAsync(string agentName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentConfigurationResponse> ResetToolsetAsync(string agentName, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SeedGlobalDefaultsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }
}
