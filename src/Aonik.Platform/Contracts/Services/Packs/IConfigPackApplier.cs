using Aonik.Platform.Contracts.Models.Packs;

namespace Aonik.Platform.Contracts.Services.Packs;

/// <summary>
/// Applies a business-type config pack to a tenant at provision time (Spec 065): tenant-scoped
/// settings, agent overrides, and reference data, written through the existing stores. Idempotent
/// and additive-only — it inserts genuinely-new configuration and never overwrites a value that
/// already exists, so admin edits are inherently safe. The applier scopes each write to the target
/// tenant explicitly (agent overrides via a set/restore of the ambient tenant context).
/// </summary>
public interface IConfigPackApplier
{
    Task<ConfigPackResult> ApplyAsync(Guid tenantId, string businessType, CancellationToken cancellationToken = default);
}
