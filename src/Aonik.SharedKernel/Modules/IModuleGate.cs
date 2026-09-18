namespace Aonik.SharedKernel.Modules;

/// <summary>
/// The in-process module gate (Spec 097 §11, §12): the check a code path runs itself when it learns
/// which tenant it is acting for <em>after</em> the HTTP gate has already run. Implemented in Platform
/// over <see cref="IModuleEnablementReader"/>; consumed by module services through this contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <c>ModuleEnablementMiddleware</c> gates every routed endpoint against the
/// ambient tenant, and deliberately passes a request through when no tenant has been resolved,
/// because the tenant middleware owns the anonymous decision. Anonymous provider callbacks — a
/// partner payout webhook, an account-aggregator webhook, the sandbox tool callback — arrive with no
/// bearer token, no <c>X-Tenant-Id</c> and no tenant subdomain, then resolve the owning tenant from
/// the payload or from the entity the payload references. By then the middleware has already let
/// the request in. Such a processor MUST call <see cref="EnsureEnabledAsync"/> the moment it knows the
/// owning tenant and before it mutates anything, so a tenant with the module off is refused with the
/// same <c>403 { code: "module.disabled", moduleId }</c> the middleware would have produced.
/// </para>
/// <para>
/// <b>Semantics.</b> A core module, or an id the catalogue does not know, is always enabled: the gate
/// never throws for those, so a caller can pass its own module id unconditionally. The tenant id is the
/// tenant the caller <em>resolved</em>, never the ambient one — that is the whole point.
/// <see cref="Guid.Empty"/> is a programming error (the caller has not actually resolved a tenant) and
/// is rejected with an <see cref="ArgumentException"/> rather than answered.
/// </para>
/// </remarks>
public interface IModuleGate
{
    /// <summary>
    /// Throws <see cref="ModuleDisabledException"/> when <paramref name="moduleId"/> is a known non-core
    /// module that resolves disabled for <paramref name="tenantId"/>. No-op for core or unknown ids.
    /// </summary>
    Task EnsureEnabledAsync(Guid tenantId, string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when <paramref name="moduleId"/> resolves enabled for <paramref name="tenantId"/>; always
    /// true for core or unknown ids.
    /// </summary>
    Task<bool> IsEnabledAsync(Guid tenantId, string moduleId, CancellationToken cancellationToken = default);
}
