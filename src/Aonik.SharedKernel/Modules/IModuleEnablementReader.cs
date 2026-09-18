namespace Aonik.SharedKernel.Modules;

/// <summary>
/// The read side of per-tenant module enablement (Spec 097 §7). Implemented in Platform; consumed by
/// the HTTP gate, the manifest, the agent resolver and Worker jobs through this contract so none of
/// them reach into Platform's persistence.
/// </summary>
/// <remarks>
/// The result is always dependency-consistent (see
/// <see cref="ModuleCatalog.ResolveEnabled(IReadOnlyDictionary{string, bool})"/>), so callers never
/// need to reason about the graph. A database failure surfaces as an exception: the reader never
/// answers "all on" or "all off" on an error — that is the caller's decision.
/// </remarks>
public interface IModuleEnablementReader
{
    /// <summary>
    /// Resolves the enabled module set for <paramref name="tenantId"/>. Works for ANY tenant, not
    /// only the ambient one — host admins read other tenants and Worker jobs run with no tenant.
    /// </summary>
    Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Of <paramref name="tenantIds"/>, returns those for which <paramref name="moduleId"/> resolves
    /// enabled, in the order first given (duplicates collapsed). A core module yields every tenant.
    /// Intended for job fan-out: one round-trip for the whole list, never one per tenant.
    /// </summary>
    Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
        IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default);
}
