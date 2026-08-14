using Microsoft.EntityFrameworkCore;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Persistence;
using Aonik.Platform.Persistence;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Contracts.Models.Seeding;

namespace Aonik.Platform.Services.Seeding.Phases;

/// <summary>
/// Writes and removes the Settings-table markers that record what was seeded
/// for a tenant (bill-collection marker at Phase 9, cross-border marker at
/// Phase 17, and snapshot capture before Phase 10).
/// Also handles restoring the tenant profile during reversal.
/// </summary>
internal sealed class SeedMarkerPhase
{
    private const string DemoSeedKey = "DemoSeed.BillPayment";
    private const string CrossBorderDemoSeedKey = "DemoSeed.CrossBorderPayments";

    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IEnumerable<IDemoSeedContributor> _contributors;

    public SeedMarkerPhase(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<IDemoSeedContributor> contributors)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _contributors = contributors;
    }

    public async Task<TenantSnapshot> CaptureTenantSnapshotAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        return new TenantSnapshot(
            tenant.Country,
            tenant.DefaultCurrency,
            tenant.City,
            tenant.StateProvince,
            tenant.AddressLine1,
            tenant.SupportedCountriesJson,
            tenant.AllowedOriginCountriesJson,
            tenant.AllowedDestinationCountriesJson);
    }

    public async Task UpsertMarkerAsync(
        Guid tenantId,
        (Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId) partyIds,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var financeResults = GetContributorResults("Finance");
        var agentsResults = GetContributorResults("Agents");
        var platformResults = GetContributorResults("Platform");

        var payload = new
        {
            TenantId = tenantId,
            SeedType = DemoSeedTypes.BillCollection,
            UtilitiesCategoryId = GetGuid(financeResults, DemoSeedResultKeys.UtilitiesCategoryId),
            EcgBillerId = GetGuid(financeResults, DemoSeedResultKeys.EcgBillerId),
            WaterBillerId = GetGuid(financeResults, DemoSeedResultKeys.WaterBillerId),
            EcgServiceId = GetGuid(financeResults, DemoSeedResultKeys.EcgServiceId),
            WaterServiceId = GetGuid(financeResults, DemoSeedResultKeys.WaterServiceId),
            partyIds.PayerPartyId,
            partyIds.ReceiverPartyId,
            partyIds.RelationshipId,
            FxQuoteId = GetGuid(financeResults, DemoSeedResultKeys.FxQuoteId),
            FeePolicyId = GetGuid(financeResults, DemoSeedResultKeys.FeePolicyId),
            LimitsPolicyId = GetGuid(financeResults, DemoSeedResultKeys.LimitsPolicyId),
            OrderIds = GetGuidList(financeResults, DemoSeedResultKeys.OrderIds),
            AgentIdsByName = GetObject(agentsResults, DemoSeedResultKeys.AgentIdsByName),
            WorkflowIdsBySlug = GetObject(agentsResults, DemoSeedResultKeys.WorkflowIdsBySlug),
            AgentRunIds = GetObject(agentsResults, DemoSeedResultKeys.AgentRunIds),
            ProposalIds = GetObject(agentsResults, DemoSeedResultKeys.ProposalIds),
            NotificationIds = GetObject(platformResults, DemoSeedResultKeys.NotificationIds)
        };
        var value = JsonSerializer.Serialize(payload);

        var setting = await _dbContext.Settings
            .FirstOrDefaultAsync(item => item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == DemoSeedKey,
                cancellationToken);

        if (setting == null)
        {
            setting = new Setting
            {
                Key = DemoSeedKey,
                Value = value,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Settings.Add(setting);
            operations.Add("Demo seed marker created");
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = now;
            setting.UpdatedBy = userId;
            operations.Add("Demo seed marker updated");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertCrossBorderMarkerAsync(
        Guid tenantId,
        string seedType,
        TenantSnapshot tenantSnapshot,
        (Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId) billCollectionParties,
        (IReadOnlyList<Guid> CountryIds, IReadOnlyList<Guid> CurrencyIds) tenantCoverage,
        (IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds) crossBorderParties,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var payload = new
        {
            TenantId = tenantId,
            SeedType = seedType,
            TenantSnapshot = tenantSnapshot,
            BillCollection = new
            {
                billCollectionParties.PayerPartyId,
                billCollectionParties.ReceiverPartyId,
                billCollectionParties.RelationshipId
            },
            CrossBorder = new
            {
                CountryIds = tenantCoverage.CountryIds,
                CurrencyIds = tenantCoverage.CurrencyIds,
                PartyIds = crossBorderParties.PartyIds,
                RelationshipIds = crossBorderParties.RelationshipIds,
                billCollectionParties.PayerPartyId,
                billCollectionParties.ReceiverPartyId,
                billCollectionParties.RelationshipId
            }
        };

        var settingValue = JsonSerializer.Serialize(payload);
        var setting = await _dbContext.Settings
            .FirstOrDefaultAsync(item => item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == CrossBorderDemoSeedKey,
                cancellationToken);

        if (setting == null)
        {
            setting = new Setting
            {
                Key = CrossBorderDemoSeedKey,
                Value = settingValue,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Settings.Add(setting);
            operations.Add("Cross-border demo seed marker created");
        }
        else
        {
            setting.Value = settingValue;
            setting.UpdatedAt = now;
            setting.UpdatedBy = userId;
            operations.Add("Cross-border demo seed marker updated");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Setting?> GetSettingAsync(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        return await _dbContext.Settings
            .FirstOrDefaultAsync(item => !item.IsDeleted
                                         && item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == key,
                cancellationToken);
    }

    public async Task RestoreTenantProfileAsync(Guid tenantId, Setting? crossBorderSetting, List<string> operations, CancellationToken cancellationToken)
    {
        if (crossBorderSetting == null || string.IsNullOrWhiteSpace(crossBorderSetting.Value))
        {
            return;
        }

        using var document = JsonDocument.Parse(crossBorderSetting.Value);
        if (!document.RootElement.TryGetProperty("TenantSnapshot", out var snapshotElement))
        {
            return;
        }

        var snapshot = JsonSerializer.Deserialize<TenantSnapshot>(snapshotElement.GetRawText());
        if (snapshot == null)
        {
            return;
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found.");
        }

        tenant.Country = snapshot.Country ?? string.Empty;
        tenant.DefaultCurrency = snapshot.DefaultCurrency ?? string.Empty;
        tenant.City = snapshot.City;
        tenant.StateProvince = snapshot.StateProvince;
        tenant.AddressLine1 = snapshot.AddressLine1;
        tenant.SupportedCountriesJson = snapshot.SupportedCountriesJson ?? string.Empty;
        tenant.AllowedOriginCountriesJson = snapshot.AllowedOriginCountriesJson ?? string.Empty;
        tenant.AllowedDestinationCountriesJson = snapshot.AllowedDestinationCountriesJson ?? string.Empty;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Restored tenant home-base settings from pre-demo snapshot");
    }

    public async Task RemoveSeedMarkersAsync(Guid tenantId, Setting? billCollectionSetting, Setting? crossBorderSetting, List<string> operations, CancellationToken cancellationToken)
    {
        if (billCollectionSetting != null || crossBorderSetting != null)
        {
            await _dbContext.Settings
                .IncludeSoftDeleted()
                .Where(item => !item.IsDeleted
                               && item.Scope == SettingScope.Tenant
                               && item.TenantId == tenantId
                               && (item.Key == DemoSeedKey || item.Key == CrossBorderDemoSeedKey))
                .ExecuteDeleteAsync(cancellationToken);

            operations.Add("Removed demo seed markers");
        }
    }

    // ── Contributor result helpers ────────────────────────────────────

    private IReadOnlyDictionary<string, object> GetContributorResults(string moduleName)
        => _contributors.FirstOrDefault(c => c.ModuleName == moduleName)?.GetResults()
            ?? new Dictionary<string, object>();

    private static Guid GetGuid(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? (Guid)value : Guid.Empty;

    private static IReadOnlyList<Guid> GetGuidList(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? (IReadOnlyList<Guid>)value : Array.Empty<Guid>();

    private static object? GetObject(IReadOnlyDictionary<string, object> results, string key)
        => results.TryGetValue(key, out var value) ? value : null;

    // ── Snapshot record type ──────────────────────────────────────────

    internal sealed record TenantSnapshot(
        string? Country,
        string? DefaultCurrency,
        string? City,
        string? StateProvince,
        string? AddressLine1,
        string? SupportedCountriesJson,
        string? AllowedOriginCountriesJson,
        string? AllowedDestinationCountriesJson);
}
