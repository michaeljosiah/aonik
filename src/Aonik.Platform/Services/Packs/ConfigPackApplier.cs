using System.Text.Json;
using Aonik.Platform.Contracts.Models.ReferenceData;
using Aonik.Platform.Contracts.Services.Packs;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Modules;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Packs;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Packs;

/// <summary>
/// Applies a business-type config pack to a tenant at provision time (Spec 065). Additive-only:
/// settings are inserted only when the key is not already present for the tenant (mirroring
/// <c>SettingsSeedService</c>'s "never overwrite" rule), so admin edits are inherently safe. Agent
/// overrides go through <see cref="IAgentConfigurationService"/>, which resolves its target tenant
/// from the ambient <see cref="ITenantContext"/> — so the applier sets and restores the context
/// around those writes (the provisioner runs in an admin/bootstrap scope pinned to no single tenant).
/// The module rows (Spec 097 §13) follow the same additive-only rule: see <see cref="ApplyModulesAsync"/>.
/// </summary>
internal sealed class ConfigPackApplier : IConfigPackApplier
{
    private readonly IConfigPackSource _source;
    private readonly PlatformDbContext _dbContext;
    private readonly IAgentConfigurationService _agentConfig;
    private readonly IReferenceDataService _referenceData;
    private readonly ITenantContext _tenantContext;
    private readonly TenantModuleService? _moduleService;

    /// <param name="moduleService">
    /// The Platform-internal enablement service, used only to drop its cache after module rows are
    /// written so the provisioner's very next read sees the pack's module set. Optional so hosts and
    /// tests that build the applier without the module graph still work (they simply skip invalidation).
    /// </param>
    public ConfigPackApplier(
        IConfigPackSource source,
        PlatformDbContext dbContext,
        IAgentConfigurationService agentConfig,
        IReferenceDataService referenceData,
        ITenantContext tenantContext,
        TenantModuleService? moduleService = null)
    {
        _source = source;
        _dbContext = dbContext;
        _agentConfig = agentConfig;
        _referenceData = referenceData;
        _tenantContext = tenantContext;
        _moduleService = moduleService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ApplyModulesAsync(Guid tenantId, string businessType, bool initialProvisioning, CancellationToken cancellationToken = default)
    {
        var manifest = _source.Get(businessType);
        if (manifest is null || manifest.Modules.Count == 0)
        {
            return Array.Empty<string>(); // base / unknown type, or a pack with no module opinion: catalogue defaults
        }

        // The source validates ids on load; re-check here so a manifest from any other source cannot
        // write a row for an id the catalogue does not know (the reader would silently ignore it).
        var unknown = manifest.Modules.Where(id => !ModuleCatalog.IsKnown(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"Config pack '{manifest.BusinessType}' declares module(s) not in the catalogue: {string.Join(", ", unknown)}.");
        }

        // Spec 097 §13: declared modules + their transitive hard dependencies + core are on; the rest off.
        var enabledSet = new HashSet<string>(ModuleCatalog.CoreIds, StringComparer.Ordinal);
        enabledSet.UnionWith(ModuleCatalog.HardDependencyClosure(manifest.Modules));

        var reason = $"pack:{manifest.BusinessType}@v{manifest.Version}";

        // Writes target the provisioned tenant, which is not necessarily the ambient one (the provisioner
        // runs in an admin/bootstrap scope). Pin the context so the DbContext's tenant write-guard accepts
        // both the inserts and any flip of an existing row, and restore it afterwards.
        var priorTenant = _tenantContext.TenantId;
        var priorSource = _tenantContext.ResolutionSource;
        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = "ConfigPackApplier";

        try
        {
            // Explicit tenant filter rather than the ambient one, for the same reason (see TenantModuleService).
            var existingRows = await _dbContext.TenantModules
                .AcrossTenants()
                .Where(row => !row.IsDeleted && row.TenantId == tenantId)
                .ToListAsync(cancellationToken);
            var existingByModule = existingRows.ToDictionary(row => row.ModuleId, StringComparer.Ordinal);

            var dirty = false;

            foreach (var descriptor in ModuleCatalog.All)
            {
                var shouldEnable = enabledSet.Contains(descriptor.Id);

                if (!existingByModule.TryGetValue(descriptor.Id, out var row))
                {
                    // A disabling row is written ONLY while the tenant is being provisioned for the first
                    // time. A tenant that already existed before its rows did (pre-Spec-097, or provisioned
                    // by a pack that had no module opinion yet) resolves to "everything on", and a re-run of
                    // provisioning must never narrow that: on the additive path an undeclared module simply
                    // gets no row, so it keeps the catalogue default.
                    if (!shouldEnable && !initialProvisioning)
                    {
                        continue;
                    }

                    _dbContext.TenantModules.Add(new TenantModule
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ModuleId = descriptor.Id,
                        IsEnabled = shouldEnable,
                        Source = TenantModuleSource.Pack,
                        Reason = reason,
                    });
                    dirty = true;
                    continue;
                }

                // Additive-only on re-apply (mirrors the settings rule): a host admin's explicit row is
                // never touched, and a pack-sourced row is only ever flipped off to on, so a newer pack
                // version can widen a tenant's module set but never narrow it.
                if (!string.Equals(row.Source, TenantModuleSource.Pack, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!row.IsEnabled && shouldEnable)
                {
                    row.IsEnabled = true;
                    row.Reason = reason;
                    dirty = true;
                }
            }

            if (dirty)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _tenantContext.TenantId = priorTenant;
            _tenantContext.ResolutionSource = priorSource;
        }

        if (_moduleService is not null)
        {
            await _moduleService.InvalidateAsync(tenantId, cancellationToken);
        }

        var enabledList = string.Join(", ", ModuleCatalog.All.Select(descriptor => descriptor.Id).Where(enabledSet.Contains));
        return initialProvisioning
            ? new[] { $"Applied module set from config pack '{manifest.BusinessType}' v{manifest.Version}: enabled {enabledList}" }
            : new[] { $"Re-applied config pack '{manifest.BusinessType}' v{manifest.Version} additively: ensured enabled {enabledList}; existing module defaults preserved" };
    }

    public async Task<ConfigPackResult> ApplyAsync(Guid tenantId, string businessType, CancellationToken cancellationToken = default)
    {
        var manifest = _source.Get(businessType);
        if (manifest is null)
        {
            return ConfigPackResult.None; // base / unknown type → nothing to apply
        }

        var actions = new List<string>();

        await ApplySettingsAsync(tenantId, manifest, actions, cancellationToken);
        await ApplyReferenceDataAsync(tenantId, manifest, actions, cancellationToken);
        await ApplyAgentOverridesAsync(tenantId, manifest, actions, cancellationToken);

        // Record what was applied. The tenant is already tracked by the provisioner's shared
        // PlatformDbContext; re-fetch returns that tracked instance.
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is not null)
        {
            tenant.AppliedPackVersion = manifest.Version;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        actions.Add($"Applied config pack '{manifest.BusinessType}' v{manifest.Version}");
        return new ConfigPackResult(manifest.Version, actions);
    }

    /// <summary>Additive-only: insert tenant settings for keys not already present (never overwrite).</summary>
    private async Task ApplySettingsAsync(Guid tenantId, ConfigPackManifest manifest, List<string> actions, CancellationToken cancellationToken)
    {
        if (manifest.Settings.Count == 0)
        {
            return;
        }

        var existingKeys = await _dbContext.Settings
            .Where(s => s.Scope == SettingScope.Tenant && s.TenantId == tenantId)
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var existing = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);

        var toAdd = manifest.Settings
            .Where(kv => !existing.Contains(kv.Key))
            .Select(kv => new Setting
            {
                Key = kv.Key,
                Value = kv.Value,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _dbContext.Settings.AddRangeAsync(toAdd, cancellationToken);
            actions.Add($"Applied {toAdd.Count} tenant setting(s)");
        }
    }

    private async Task ApplyReferenceDataAsync(Guid tenantId, ConfigPackManifest manifest, List<string> actions, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in manifest.ReferenceData)
        {
            // Additive-only: only insert codes the tenant does not already have, so a re-apply never
            // overwrites reference data an admin edited (display name / sort order / active flag).
            var existing = await _referenceData.GetAsync(group.Type, tenantId, cancellationToken);
            var existingCodes = new HashSet<string>(existing.Select(e => e.Code), StringComparer.OrdinalIgnoreCase);

            foreach (var item in group.Items)
            {
                if (existingCodes.Contains(item.Code))
                {
                    continue;
                }

                await _referenceData.UpsertAsync(
                    new ReferenceDataItemUpsert(group.Type, item.Code, item.DisplayName, item.SortOrder, IsActive: true),
                    tenantId,
                    cancellationToken);
                count++;
            }
        }

        if (count > 0)
        {
            actions.Add($"Applied {count} reference-data item(s)");
        }
    }

    /// <summary>
    /// Agent overrides target the tenant resolved from the ambient <see cref="ITenantContext"/>, so we
    /// pin the context to the provisioned tenant for the duration and restore it afterwards — the
    /// provisioner runs in a scope not pinned to this tenant, and later steps (audit log) rely on it.
    /// </summary>
    private async Task ApplyAgentOverridesAsync(Guid tenantId, ConfigPackManifest manifest, List<string> actions, CancellationToken cancellationToken)
    {
        if (manifest.Agents.Count == 0)
        {
            return;
        }

        var priorTenant = _tenantContext.TenantId;
        var priorSource = _tenantContext.ResolutionSource;
        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = "ConfigPackApplier";

        var applied = 0;

        try
        {
            foreach (var agent in manifest.Agents)
            {
                if (string.IsNullOrWhiteSpace(agent.Name))
                {
                    continue;
                }

                try
                {
                    // Additive-only: never overwrite an existing TENANT override (e.g. an admin-edited
                    // persona). The ambient tenant is pinned above, so a resolved row whose TenantId is
                    // this tenant means an override already exists — leave it untouched (Codex review).
                    var resolved = await _agentConfig.GetResolvedAsync(agent.Name, cancellationToken);
                    if (resolved?.TenantId == tenantId)
                    {
                        continue;
                    }

                    var request = new UpsertAgentConfigurationRequest
                    {
                        InstructionsText = agent.InstructionsText,
                        ToolsetIdsJson = agent.Toolset is { Count: > 0 } tools ? JsonSerializer.Serialize(tools) : null,
                        ModelId = agent.ModelId,
                    };

                    await _agentConfig.UpsertOverrideAsync(agent.Name, request, cancellationToken);
                    applied++;
                }
                catch (ModuleDisabledException ex)
                {
                    // Spec 097 §12.1: the agent configuration service refuses a code-based agent whose
                    // module is off for this tenant. A pack that declares an override for such an agent
                    // is not a provisioning failure — the override is simply not applicable here.
                    actions.Add($"Skipped agent override '{agent.Name}': module '{ex.ModuleId}' disabled for tenant");
                }
            }
        }
        finally
        {
            _tenantContext.TenantId = priorTenant;
            _tenantContext.ResolutionSource = priorSource;
        }

        actions.Add($"Applied {applied} agent override(s)");
    }
}
