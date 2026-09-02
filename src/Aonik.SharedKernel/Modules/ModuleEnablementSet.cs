namespace Aonik.SharedKernel.Modules;

/// <summary>
/// The resolved, dependency-consistent set of enabled modules for one tenant (Spec 097 §7).
/// </summary>
public sealed class ModuleEnablementSet
{
    public ModuleEnablementSet(Guid tenantId, IReadOnlySet<string> enabled)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        TenantId = tenantId;
        Enabled = enabled;
    }

    public Guid TenantId { get; }

    /// <summary>The catalogue ids that resolved enabled — core modules are always present.</summary>
    public IReadOnlySet<string> Enabled { get; }

    public bool IsEnabled(string moduleId) => Enabled.Contains(moduleId);
}
