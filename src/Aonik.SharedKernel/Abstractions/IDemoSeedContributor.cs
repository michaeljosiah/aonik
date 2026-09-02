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
    /// The owning module's canonical catalogue id — a <see cref="Modules.ModuleIds"/> constant such as
    /// <c>ModuleIds.Finance</c>. The demo seed gates on it through <see cref="Modules.ModuleCatalog"/>
    /// (Spec 097 §12.4): a contributor whose module is disabled for the tenant is skipped.
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
    CrossBorderPricing,

    /// <summary>
    /// Demo activity — orders + items, agent runs, proposals, notifications.
    /// Runs after all the catalog/pricing/workflows phases so it can reference
    /// every seeded entity by id. Populates the user-visible activity pages
    /// (/orders, /approvals, /ai/runs, the notifications bell) so a fresh
    /// demo install isn't a sea of empty tables.
    /// </summary>
    Activity
}

/// <summary>
/// Context passed to demo seed contributors.
/// </summary>
public record DemoSeedContext(
    Guid TenantId,
    string SeedType,
    DateTime Now,
    Guid? UserId,
    // Spec 065 — the tenant's business type; sample contributors gate their content on it.
    // Config comes from the config pack at provision, so the demo seed adds sample CONTENT only.
    string BusinessType = BusinessTypes.Base
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

    // Activity phase results
    public const string OrderIds = "OrderIds";
    public const string AgentRunIds = "AgentRunIds";
    public const string ProposalIds = "ProposalIds";
    public const string NotificationIds = "NotificationIds";
}
