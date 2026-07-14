using System.Text.Json;
using Aonik.Platform.Contracts.Models.Packs;
using Aonik.Platform.Contracts.Models.ReferenceData;
using Aonik.Platform.Contracts.Services.Packs;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Packs;

/// <summary>
/// Applies a business-type config pack to a tenant at provision time (Spec 065). Additive-only:
/// settings are inserted only when the key is not already present for the tenant (mirroring
/// <c>SettingsSeedService</c>'s "never overwrite" rule), so admin edits are inherently safe. Agent
/// overrides go through <see cref="IAgentConfigurationService"/>, which resolves its target tenant
/// from the ambient <see cref="ITenantContext"/> — so the applier sets and restores the context
/// around those writes (the provisioner runs in an admin/bootstrap scope pinned to no single tenant).
/// </summary>
internal sealed class ConfigPackApplier : IConfigPackApplier
{
    private readonly IConfigPackSource _source;
    private readonly PlatformDbContext _dbContext;
    private readonly IAgentConfigurationService _agentConfig;
    private readonly IReferenceDataService _referenceData;
    private readonly ITenantContext _tenantContext;

    public ConfigPackApplier(
        IConfigPackSource source,
        PlatformDbContext dbContext,
        IAgentConfigurationService agentConfig,
        IReferenceDataService referenceData,
        ITenantContext tenantContext)
    {
        _source = source;
        _dbContext = dbContext;
        _agentConfig = agentConfig;
        _referenceData = referenceData;
        _tenantContext = tenantContext;
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
            foreach (var item in group.Items)
            {
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

        try
        {
            foreach (var agent in manifest.Agents)
            {
                if (string.IsNullOrWhiteSpace(agent.Name))
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
            }
        }
        finally
        {
            _tenantContext.TenantId = priorTenant;
            _tenantContext.ResolutionSource = priorSource;
        }

        actions.Add($"Applied {manifest.Agents.Count} agent override(s)");
    }
}
