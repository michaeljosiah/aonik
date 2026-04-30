namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for contributing to demo/seed data provisioning.
/// Each module (Finance, AI, etc.) implements this to seed its own data
/// when demo data is provisioned for a tenant.
///
/// Resolved as <c>IEnumerable&lt;IDemoSeedContributor&gt;</c>
/// by DemoSeedService.
/// </summary>
public interface IDemoSeedContributor
{
    /// <summary>
    /// Module name for logging/diagnostics (e.g. "Finance").
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Seeds module-specific data for the given phase.
    /// Called by DemoSeedService at the appropriate point in the seeding pipeline.
    /// Implementations should no-op for phases they don't handle.
    /// Returns a list of operation descriptions for audit logging.
    /// </summary>
    Task<IReadOnlyList<string>> SeedAsync(DemoSeedPhase phase, DemoSeedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears change-tracking state on the module's DbContext.
    /// Called between seeding phases to prevent tracker bloat.
    /// </summary>
    void ClearTracking();

    /// <summary>
    /// Returns named results collected across phases, keyed by a well-known name.
    /// Used by the orchestrator to build seed markers that reference cross-module IDs.
    /// </summary>
    IReadOnlyDictionary<string, object> GetResults();
}

/// <summary>
/// Phases in the demo seeding pipeline. DemoSeedService calls contributors
/// at each phase in the order defined here.
/// </summary>
public enum DemoSeedPhase
{
    /// <summary>CatalogSeedService categories (biller categories)</summary>
    CatalogCategories,

    /// <summary>Bill-collection partner + prefund account</summary>
    BillCollectionPartner,

    /// <summary>Demo catalog (billers + services)</summary>
    Catalog,

    /// <summary>Pricing (FX quotes, fee policies, limits policies)</summary>
    Pricing,

    /// <summary>
    /// Agents fleet + workflow registry (Agents module). Seeds the seven
    /// domain agents the workflows reference, then the workflow rows + nodes
    /// + edges + comments + version history + recent runs.
    /// </summary>
    Workflows,

    /// <summary>Cross-border partner network</summary>
    CrossBorderPartnerNetwork,

    /// <summary>Cross-border catalog</summary>
    CrossBorderCatalog,

    /// <summary>Households (personal finance)</summary>
    Households,

    /// <summary>Cross-border pricing</summary>
    CrossBorderPricing
}

/// <summary>
/// Context passed to demo seed contributors.
/// </summary>
public record DemoSeedContext(
    Guid TenantId,
    string SeedType,
    DateTime Now,
    Guid? UserId
);

/// <summary>
/// Well-known result keys returned by demo seed contributors.
/// </summary>
public static class DemoSeedResultKeys
{
    // Bill collection phase results
    public const string BillCollectionPartnerId = "BillCollectionPartnerId";

    // Catalog phase results
    public const string UtilitiesCategoryId = "UtilitiesCategoryId";
    public const string EcgBillerId = "EcgBillerId";
    public const string WaterBillerId = "WaterBillerId";
    public const string EcgServiceId = "EcgServiceId";
    public const string WaterServiceId = "WaterServiceId";

    // Pricing phase results
    public const string FxQuoteId = "FxQuoteId";
    public const string FeePolicyId = "FeePolicyId";
    public const string LimitsPolicyId = "LimitsPolicyId";

    // Cross-border partner network results
    public const string PartnerIdsByCountry = "PartnerIdsByCountry";
    public const string ConnectorIdsByCountry = "ConnectorIdsByCountry";

    // Cross-border catalog results
    public const string CrossBorderCategoryIds = "CrossBorderCategoryIds";
    public const string CrossBorderBillerIds = "CrossBorderBillerIds";
    public const string CrossBorderServiceIds = "CrossBorderServiceIds";

    // Household results
    public const string HouseholdIds = "HouseholdIds";
    public const string HouseholdMemberIds = "HouseholdMemberIds";

    // Cross-border pricing results
    public const string CrossBorderFxQuoteIds = "CrossBorderFxQuoteIds";
    public const string CrossBorderFeePolicyIds = "CrossBorderFeePolicyIds";
    public const string CrossBorderLimitsPolicyIds = "CrossBorderLimitsPolicyIds";

    // Workflow phase results
    public const string AgentIdsByName = "AgentIdsByName";
    public const string WorkflowIdsBySlug = "WorkflowIdsBySlug";
}
