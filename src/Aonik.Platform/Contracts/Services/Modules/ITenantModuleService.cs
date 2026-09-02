using Aonik.Platform.Contracts.Models.Modules;

namespace Aonik.Platform.Contracts.Services.Modules;

/// <summary>
/// The admin-facing side of per-tenant module enablement (Spec 097 §9): reads the full catalogue
/// with state, and applies host-admin toggles with dependency validation, audit and event publication.
/// The cached, dependency-closed read that gates and the manifest consume is
/// <see cref="Aonik.SharedKernel.Modules.IModuleEnablementReader"/>, not this contract.
/// </summary>
public interface ITenantModuleService
{
    /// <summary>
    /// Every catalogue module with the tenant's resolved state. A caller reading a tenant other than
    /// the ambient one needs the <c>Tenants.Read</c> permission, the same guard the feature endpoints use.
    /// </summary>
    Task<TenantModuleList> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies <paramref name="toggles"/> atomically. Throws <see cref="ArgumentException"/> for an
    /// unknown, core or duplicated id, and <see cref="Aonik.SharedKernel.Modules.ModuleDependencyException"/>
    /// when the result would violate the hard-dependency graph (nothing is cascaded silently). On
    /// success the change is audited, <see cref="Aonik.SharedKernel.Events.Integration.TenantModulesChangedEvent"/>
    /// is published and the tenant's cached set is invalidated. Authorisation is the endpoint's
    /// (PlatformAdmin policy); this method carries no tenancy guard of its own.
    /// </summary>
    Task<TenantModuleList> UpdateAsync(
        Guid tenantId,
        IReadOnlyList<TenantModuleToggle> toggles,
        CancellationToken cancellationToken = default);
}
