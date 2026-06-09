using Aonik.Finance.Contracts.Models.Partners;

namespace Aonik.Finance.Contracts.Services.Partners;

/// <summary>
/// Admin surface for partner-owned credential bundles + the connector registry schema (Spec 042 §12).
/// All operations are admin-gated and tenant-scoped; secret values are never returned.
/// </summary>
public interface ICredentialBundleAdminService
{
    /// <summary>The code-owned connector kinds + their credential / config schemas (drives the editor form).</summary>
    Task<IReadOnlyList<ConnectorKindSchemaDto>> GetConnectorKindsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists this tenant's bundles with field state and the connectors that bind each.</summary>
    Task<IReadOnlyList<CredentialBundleListItem>> ListBundlesAsync(CancellationToken cancellationToken = default);

    Task<CredentialBundleListItem> CreateBundleAsync(
        CreateCredentialBundleRequest request, CancellationToken cancellationToken = default);

    Task<CredentialBundleListItem> UpdateBundleAsync(
        string bundleRef, UpdateCredentialBundleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Rotates one verifier field with a grace window for the previous value (Spec 042 §11).</summary>
    Task<CredentialBundleListItem> RotateFieldAsync(
        string bundleRef, RotateCredentialFieldRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently migrates the existing provider-singleton Flutterwave settings into a seeded default
    /// partner + connectors + bundles, and backfills <c>ConnectorId</c> on existing money records (Spec 042 §13).
    /// </summary>
    Task<LiftLegacyFlutterwaveResult> LiftLegacyFlutterwaveAsync(CancellationToken cancellationToken = default);
}
