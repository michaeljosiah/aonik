namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Declares, once per module assembly, which <see cref="ModuleCatalog"/> module the assembly
/// belongs to: <c>[assembly: AonikModule(ModuleIds.Commerce)]</c> (Spec 097 §5).
/// </summary>
/// <remarks>
/// Everything that needs to know "which module does this type belong to" — the HTTP gate, agent
/// descriptors, job definitions, event handlers — resolves it from <c>Type.Assembly</c> through
/// <see cref="ModuleCatalog.TryGetModuleId(Type)"/>. That is what makes gating complete by
/// construction: an endpoint cannot be forgotten, because it lives in an assembly. The host and
/// composition assemblies (Api, Application, Infrastructure, SharedKernel) carry no attribute and
/// are never gated.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class AonikModuleAttribute : Attribute
{
    public AonikModuleAttribute(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ModuleId = moduleId;
    }

    /// <summary>The catalogue id (one of <see cref="ModuleIds"/>).</summary>
    public string ModuleId { get; }
}
