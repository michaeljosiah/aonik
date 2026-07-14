namespace Aonik.SharedKernel.Abstractions.Packs;

/// <summary>
/// Resolves business-type config-pack manifests (Spec 065). First-party manifests ship as embedded
/// JSON resources; a business type with no manifest resolves to <c>null</c> (a no-op pack). Concrete
/// product types are discovered here from the installed manifests — platform code never branches on a
/// specific product (ADR-013).
/// </summary>
public interface IConfigPackSource
{
    /// <summary>The manifest for a business type, or <c>null</c> when none is installed (no-op).</summary>
    ConfigPackManifest? Get(string businessType);

    /// <summary>The business types that have an installed manifest (for discovery / tooling).</summary>
    IReadOnlyList<string> ListBusinessTypes();
}
