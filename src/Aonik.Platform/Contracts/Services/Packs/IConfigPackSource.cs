using Aonik.Platform.Contracts.Models.Packs;

namespace Aonik.Platform.Contracts.Services.Packs;

/// <summary>
/// Resolves a business-type config-pack manifest (Spec 065). First-party manifests ship as embedded
/// JSON resources; a business type with no manifest resolves to <c>null</c> (a no-op pack). The
/// concrete product types are discovered here from the installed manifests — platform code never
/// branches on a specific product (ADR-013).
/// </summary>
public interface IConfigPackSource
{
    /// <summary>The manifest for a business type, or <c>null</c> when none is installed (no-op).</summary>
    ConfigPackManifest? Get(string businessType);
}
