namespace Aonik.Platform.Contracts.Services.Packs;

/// <summary>
/// Applies a business-type config pack to a tenant at provision time (Spec 065): tenant-scoped
/// settings, agent overrides, and reference data, written through the existing stores. Idempotent
/// and additive-only — it inserts genuinely-new configuration and never overwrites a value that
/// already exists, so admin edits are inherently safe. The applier scopes each write to the target
/// tenant explicitly (agent overrides via a set/restore of the ambient tenant context). The pack
/// manifest itself lives in SharedKernel (<see cref="Aonik.SharedKernel.Abstractions.Packs.IConfigPackSource"/>).
/// </summary>
public interface IConfigPackApplier
{
    Task<ConfigPackResult> ApplyAsync(Guid tenantId, string businessType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the tenant's per-module enablement rows from the pack's <c>modules</c> list (Spec 097 §13).
    /// A pack that declares no modules is a no-op (the tenant keeps the catalogue defaults).
    /// </summary>
    /// <param name="initialProvisioning">
    /// True only while the tenant is being provisioned for the very first time (no <c>TenantModule</c> rows
    /// yet and no pack version stamped). Then the pack is authoritative: one pack-sourced row is written per
    /// catalogue module — on for the declared modules, their transitive hard dependencies and the core
    /// modules; off for everything else. False on every later run (re-provision, health, CLI pack apply):
    /// the additive path creates or flips on rows for the declared + closure + core set only, never writes a
    /// disabling row and never touches a row a host admin wrote explicitly — so a tenant that existed before
    /// its rows did keeps resolving "everything on" and a newer pack can widen but never narrow a module set.
    /// </param>
    /// <remarks>
    /// Runs BEFORE the provisioning contributors so they can be skipped for disabled modules. Returns the
    /// human action log for the provisioning audit record.
    /// </remarks>
    Task<IEnumerable<string>> ApplyModulesAsync(Guid tenantId, string businessType, bool initialProvisioning, CancellationToken cancellationToken = default);
}

/// <summary>The outcome of applying a config pack — the version applied (null if no pack) and a human action log.</summary>
public sealed record ConfigPackResult(int? AppliedVersion, IReadOnlyList<string> Actions)
{
    public static readonly ConfigPackResult None = new(null, Array.Empty<string>());
}
