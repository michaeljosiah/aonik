using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// A first-class, tenant-scoped credential bundle (ADR-010 / Spec 042 §6). It owns the encrypted secret
/// bytes a connector instance binds to via <see cref="Connector.CredentialsRef"/>. Bundles are a separate
/// entity — not a settings-store convention — because <c>SettingService</c> only encrypts statically-defined
/// keys, so a dynamic per-bundle key would persist in plaintext (Spec 042 §2 / <c>SettingService.cs:258</c>).
/// Secrets are <strong>write-only</strong>: <see cref="ProtectedSecretsJson"/> is encrypted with explicit
/// <c>IDataProtection</c> and never returned by any read API; reads expose only <see cref="FieldMetadataJson"/>
/// (which fields are set + their rotation version).
/// </summary>
public class CredentialBundle : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Immutable, opaque stable handle (e.g. <c>fw-uk-oauth</c>) that <see cref="Connector.CredentialsRef"/>
    /// stores. Chosen over the surrogate <c>Id</c> so the binding reads cleanly in logs and survives
    /// export/import, and over <see cref="Name"/> so a rename never silently re-points a connector. Unique
    /// per <c>(TenantId, Ref)</c>.
    /// </summary>
    public string Ref { get; set; } = string.Empty;

    /// <summary>Mutable display label only — never the binding reference.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The connector kind whose credential schema validates this bundle's field set (Spec 042 §4).</summary>
    public string ConnectorKind { get; set; } = string.Empty;

    /// <summary>
    /// The secret field map, serialized as a <c>CredentialSecretStore</c> and encrypted with
    /// <c>IDataProtection</c>. Decrypted only server-side at connector-build time; never returned to a client.
    /// </summary>
    public string ProtectedSecretsJson { get; set; } = string.Empty;

    /// <summary>
    /// Plaintext, value-free view of the bundle — which fields are set and their per-field rotation version
    /// (Spec 042 §6, §11). Serialized list of <c>CredentialFieldState</c>. Powers the <c>hasXxx</c> /
    /// "Configured" badges without decrypting the secrets.
    /// </summary>
    public string FieldMetadataJson { get; set; } = "[]";
}
