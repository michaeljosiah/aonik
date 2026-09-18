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
    /// when the result would violate the hard-dependency graph (nothing is cascaded silently).
    /// Every module that transitions from off to on in the resolved set (a dependency the cascade
    /// switches on included) is provisioned first — its
    /// <see cref="Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor"/>s run, dependencies
    /// before dependents — so a module is never reported enabled without the resources its endpoints
    /// assume; a throwing contributor surfaces as <see cref="ModuleProvisioningException"/>, is audited,
    /// and leaves the module state untouched. The rows, the outbox message and the audit record commit
    /// in one database transaction; only then is
    /// <see cref="Aonik.SharedKernel.Events.Integration.TenantModulesChangedEvent"/> published and the
    /// tenant's cached set invalidated. Authorisation is the endpoint's (PlatformAdmin policy); this
    /// method carries no tenancy guard of its own.
    /// </summary>
    Task<TenantModuleList> UpdateAsync(
        Guid tenantId,
        IReadOnlyList<TenantModuleToggle> toggles,
        CancellationToken cancellationToken = default);
}
