using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Seeding.Phases;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Modules;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Finance module's demo-seed contributor. Handles all Finance-domain seeding
/// (partners, catalog, pricing) that was previously embedded in
/// <c>DemoSeedService</c> and <c>CatalogSeedService</c> in the Platform module.
///
/// Per-phase logic lives in focused helpers under the <c>Phases/</c> folder.
/// This class is a thin dispatch orchestrator — one call per phase case.
///
/// Spec 027 S5 (#118/#126): the PersonalFinance phases (Households,
/// PersonalFinanceActivity) moved to <c>PersonalFinanceDemoSeedContributor</c>,
/// so Finance no longer owns them and takes no PersonalFinance reference.
/// </summary>
internal sealed class FinanceDemoSeedContributor : IDemoSeedContributor
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ILogger<FinanceDemoSeedContributor> _logger;

    // Accumulated results that the orchestrator can read via GetResults()
    private readonly Dictionary<string, object> _results = new();

    // Phase helpers
    private readonly CatalogCategoriesSeedPhase _catalogCategories;
    private readonly BillCollectionPartnerSeedPhase _billCollectionPartner;
    private readonly CatalogSeedPhase _catalog;
    private readonly PricingSeedPhase _pricing;
    private readonly CrossBorderPartnerNetworkSeedPhase _crossBorderPartnerNetwork;
    private readonly CrossBorderCatalogSeedPhase _crossBorderCatalog;
    private readonly CrossBorderPricingSeedPhase _crossBorderPricing;
    private readonly OrderActivitySeedPhase _orderActivity;

    // Primary constructor — used by DI (all phase helpers injected).
    public FinanceDemoSeedContributor(
        FinanceDbContext financeDbContext,
        ILogger<FinanceDemoSeedContributor> logger,
        CatalogCategoriesSeedPhase catalogCategories,
        BillCollectionPartnerSeedPhase billCollectionPartner,
        CatalogSeedPhase catalog,
        PricingSeedPhase pricing,
        CrossBorderPartnerNetworkSeedPhase crossBorderPartnerNetwork,
        CrossBorderCatalogSeedPhase crossBorderCatalog,
        CrossBorderPricingSeedPhase crossBorderPricing,
        OrderActivitySeedPhase orderActivity)
    {
        _financeDbContext = financeDbContext;
        _logger = logger;
        _catalogCategories = catalogCategories;
        _billCollectionPartner = billCollectionPartner;
        _catalog = catalog;
        _pricing = pricing;
        _crossBorderPartnerNetwork = crossBorderPartnerNetwork;
        _crossBorderCatalog = crossBorderCatalog;
        _crossBorderPricing = crossBorderPricing;
        _orderActivity = orderActivity;
    }

    // Legacy constructor — used by tests that construct FinanceDemoSeedContributor
    // directly without DI. Builds phase helpers inline from the provided
    // context + logger. Mirrors the legacy ctor on DemoSeedService.
    //
    // Spec 027 S5 (#118/#126): the PersonalFinance-owning phases (Households,
    // PersonalFinanceActivity) moved to PersonalFinanceDemoSeedContributor, so
    // this ctor no longer takes a PersonalFinanceDbContext.
    // internal (not public) so the DI container sees only the primary ctor
    // above — two resolvable public ctors, neither a superset of the other,
    // throws "ambiguous constructors" at activation. Tests reach this via
    // InternalsVisibleTo.
    internal FinanceDemoSeedContributor(
        FinanceDbContext financeDbContext,
        ILogger<FinanceDemoSeedContributor> logger)
        : this(
            financeDbContext,
            logger,
            new CatalogCategoriesSeedPhase(financeDbContext, NullLogger<CatalogCategoriesSeedPhase>.Instance),
            new BillCollectionPartnerSeedPhase(financeDbContext, new PartnerPrefundSeedHelper(financeDbContext)),
            new CatalogSeedPhase(financeDbContext, new CatalogUpsertHelper(financeDbContext)),
            new PricingSeedPhase(financeDbContext, new PricingUpsertHelper(financeDbContext)),
            new CrossBorderPartnerNetworkSeedPhase(financeDbContext, new PartnerPrefundSeedHelper(financeDbContext)),
            new CrossBorderCatalogSeedPhase(financeDbContext, new CatalogUpsertHelper(financeDbContext)),
            new CrossBorderPricingSeedPhase(financeDbContext, new PricingUpsertHelper(financeDbContext)),
            new OrderActivitySeedPhase(financeDbContext))
    {
    }

    public string ModuleName => ModuleIds.Finance;

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedPhase phase,
        DemoSeedContext context,
        CancellationToken cancellationToken = default)
    {
        return phase switch
        {
            DemoSeedPhase.CatalogCategories        => await _catalogCategories.SeedAsync(cancellationToken),
            DemoSeedPhase.BillCollectionPartner    => await _billCollectionPartner.SeedAsync(context, _results, cancellationToken),
            DemoSeedPhase.Catalog                  => await _catalog.SeedAsync(context, _results, cancellationToken),
            DemoSeedPhase.Pricing                  => await _pricing.SeedAsync(context, _results, cancellationToken),
            DemoSeedPhase.CrossBorderPartnerNetwork => await _crossBorderPartnerNetwork.SeedAsync(context, _results, cancellationToken),
            DemoSeedPhase.CrossBorderCatalog       => await _crossBorderCatalog.SeedAsync(context, _results, cancellationToken),
            // Households moved to PersonalFinanceDemoSeedContributor (Spec 027 S5).
            DemoSeedPhase.CrossBorderPricing       => await _crossBorderPricing.SeedAsync(context, _results, cancellationToken),
            // Activity: Finance seeds only OrderActivity now; the PersonalFinance
            // persona activity is seeded by PersonalFinanceDemoSeedContributor.
            DemoSeedPhase.Activity                 => await _orderActivity.SeedAsync(context, _results, cancellationToken),
            _                                      => Array.Empty<string>()
        };
    }

    public void ClearTracking()
    {
        _financeDbContext.ChangeTracker.Clear();
    }

    public IReadOnlyDictionary<string, object> GetResults() => _results;
}
