using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Hides domain agents whose module is disabled for the current tenant (Spec 097 §12.1).
/// A disabled module contributes no agent, so it cannot be listed, resolved by name, built for
/// the playground or delegated to by the orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// The gate keys on <see cref="IDomainAgentDescriptor.ModuleId"/>, which defaults to the
/// <see cref="AonikModuleAttribute"/> of the descriptor's assembly. Descriptors from core modules
/// (Agents, AI, Platform, Ordering) and descriptors whose module id is null or not in the
/// catalogue are never filtered — only a known, non-core module can be off.
/// </para>
/// <para>
/// When no tenant is resolved (Worker jobs, host-level tooling, unit tests) or the reader is not
/// registered in the host, every descriptor is returned. The reader is optional so hosts that
/// compose the Agents module without Platform keep working unchanged.
/// </para>
/// </remarks>
internal sealed class DescriptorModuleFilter
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IModuleEnablementReader? _reader;
    private readonly ILogger<DescriptorModuleFilter> _logger;

    public DescriptorModuleFilter(
        ITenantProvider tenantProvider,
        ILogger<DescriptorModuleFilter> logger,
        IModuleEnablementReader? reader = null)
    {
        _tenantProvider = tenantProvider;
        _logger = logger;
        _reader = reader;
    }

    /// <summary>
    /// True when <paramref name="moduleId"/> names a catalogue module that a tenant can switch
    /// off — i.e. a known, non-core module.
    /// </summary>
    public static bool IsGated(string? moduleId)
        => moduleId is not null && ModuleCatalog.IsKnown(moduleId) && !ModuleCatalog.CoreIds.Contains(moduleId);

    /// <summary>
    /// Returns <paramref name="descriptors"/> minus those whose module is disabled for the
    /// current tenant. Order is preserved.
    /// </summary>
    public async Task<IReadOnlyList<IDomainAgentDescriptor>> FilterAsync(
        IEnumerable<IDomainAgentDescriptor> descriptors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var all = descriptors.ToList();
        if (!all.Any(descriptor => IsGated(descriptor.ModuleId)))
            return all;

        var enablement = await TryResolveEnablementAsync(cancellationToken);
        if (enablement is null)
            return all;

        var kept = new List<IDomainAgentDescriptor>(all.Count);
        foreach (var descriptor in all)
        {
            if (IsGated(descriptor.ModuleId) && !enablement.IsEnabled(descriptor.ModuleId!))
            {
                _logger.LogDebug(
                    "Hiding agent '{AgentName}' for tenant {TenantId}: module '{ModuleId}' is disabled.",
                    descriptor.Name, enablement.TenantId, descriptor.ModuleId);
                continue;
            }

            kept.Add(descriptor);
        }

        return kept;
    }

    /// <summary>
    /// Finds the descriptor named <paramref name="agentName"/> (case-insensitive). Returns null
    /// when no descriptor carries that name; throws <see cref="ModuleDisabledException"/> when one
    /// does but its module is disabled for the current tenant, so the caller surfaces the real
    /// reason (403 <c>module.disabled</c>) instead of "unknown agent".
    /// </summary>
    public async Task<IDomainAgentDescriptor?> FindAsync(
        IEnumerable<IDomainAgentDescriptor> descriptors,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var descriptor = descriptors.FirstOrDefault(
            candidate => string.Equals(candidate.Name, agentName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null || !IsGated(descriptor.ModuleId))
            return descriptor;

        var enablement = await TryResolveEnablementAsync(cancellationToken);
        if (enablement is not null && !enablement.IsEnabled(descriptor.ModuleId!))
        {
            _logger.LogDebug(
                "Refusing agent '{AgentName}' for tenant {TenantId}: module '{ModuleId}' is disabled.",
                descriptor.Name, enablement.TenantId, descriptor.ModuleId);
            throw new ModuleDisabledException(descriptor.ModuleId!);
        }

        return descriptor;
    }

    private async Task<ModuleEnablementSet?> TryResolveEnablementAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
            return null;

        // Guid.Empty is the system sentinel some hosts stamp for tenant-less work; it is not a tenant.
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty)
            return null;

        return await _reader.GetAsync(tenantId, cancellationToken);
    }
}
