using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Modules;

/// <summary>
/// Platform's <see cref="IModuleGate"/>: one dependency-consistent read through
/// <see cref="IModuleEnablementReader"/> (cached, memoised per request), then the same decision the
/// HTTP gate makes. Registered scoped so it shares the reader's per-request memo with the middleware,
/// the manifest and the agent resolver — a callback that the middleware already looked up costs
/// nothing extra here.
/// </summary>
public sealed class ModuleGate : IModuleGate
{
    private readonly IModuleEnablementReader _reader;
    private readonly ILogger<ModuleGate> _logger;

    public ModuleGate(IModuleEnablementReader reader, ILogger<ModuleGate> logger)
    {
        _reader = reader;
        _logger = logger;
    }

    public async Task EnsureEnabledAsync(Guid tenantId, string moduleId, CancellationToken cancellationToken = default)
    {
        if (await IsEnabledAsync(tenantId, moduleId, cancellationToken))
        {
            return;
        }

        _logger.LogInformation(
            "Module {ModuleId} is disabled for tenant {TenantId}; refusing a request that resolved the tenant after the HTTP gate.",
            moduleId, tenantId);

        throw new ModuleDisabledException(moduleId);
    }

    public async Task<bool> IsEnabledAsync(Guid tenantId, string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        // Core modules can never be off; an id the catalogue does not know is a build defect that the
        // assembly-enumeration test catches, not this request's problem. Neither costs a lookup.
        var descriptor = ModuleCatalog.TryGet(moduleId);
        if (descriptor is null || descriptor.IsCore)
        {
            return true;
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "The module gate needs the tenant the caller resolved; Guid.Empty means no tenant was resolved.",
                nameof(tenantId));
        }

        var enablement = await _reader.GetAsync(tenantId, cancellationToken);
        return enablement.IsEnabled(moduleId);
    }
}
