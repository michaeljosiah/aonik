using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Seeding;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Seeding;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Agents;
using System.Collections.Concurrent;

using Aonik.Platform.Services.Seeding.Phases;

namespace Aonik.Platform.Services.Seeding;

internal class DemoSeedService : IDemoSeedService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantSeedLocks = new();

    private const string DemoSeedKey = "DemoSeed.BillPayment";
    private const string CrossBorderDemoSeedKey = "DemoSeed.CrossBorderPayments";

    private readonly PlatformDbContext _dbContext;
    private readonly IEnumerable<IDemoSeedContributor> _contributors;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IPermissionService _permissionService;
    private readonly ITenantContext _tenantContext;
    private readonly IdentityRoleSeedPhase _identityRolePhase;
    private readonly PartySeedPhase _partyPhase;
    private readonly CrossBorderTenantSeedPhase _crossBorderTenantPhase;
    private readonly SeedMarkerPhase _markerPhase;
    private readonly ReverseSeedPhase _reversePhase;

    // Primary constructor — used by DI (all phase helpers injected).
    public DemoSeedService(
        PlatformDbContext dbContext,
        IEnumerable<IDemoSeedContributor> contributors,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService,
        ITenantContext tenantContext,
        FinanceDbContext financeDbContext,
        IAgentDemoCleanup agentDemoCleanup,
        IdentityRoleSeedPhase identityRolePhase,
        PartySeedPhase partyPhase,
        CrossBorderTenantSeedPhase crossBorderTenantPhase,
        SeedMarkerPhase markerPhase,
        ReverseSeedPhase reverseSeedPhase)
    {
        _dbContext = dbContext;
        _contributors = contributors;
        _clock = clock;
        _loggerFactory = loggerFactory;
        _auditLogWriter = auditLogWriter;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _permissionService = permissionService;
        _tenantContext = tenantContext;
        _identityRolePhase = identityRolePhase;
        _partyPhase = partyPhase;
        _crossBorderTenantPhase = crossBorderTenantPhase;
        _markerPhase = markerPhase;
        _reversePhase = reverseSeedPhase;
    }

    // Legacy constructor — used by tests that construct DemoSeedService directly
    // without DI. Builds phase helpers inline from the provided dependencies.
    //
    // Spec 027 S3 (#126): ReverseSeedPhase's PF teardown goes through the
    // IPersonalFinanceDemoDataReverser SharedKernel port (the PF DbSets moved to
    // PersonalFinance), so Platform needs no PersonalFinanceDbContext / PF
    // reference. Tests pass a PersonalFinanceDemoDataReverser built over an
    // InMemory PersonalFinanceDbContext.
    //
    // NOTE: internal (not public) so the DI container's greediest-resolvable
    // constructor selection sees only the primary ctor above — two resolvable
    // public ctors with neither a superset of the other throws "ambiguous
    // constructors" at activation. Tests reach this via InternalsVisibleTo.
    internal DemoSeedService(
        PlatformDbContext dbContext,
        IEnumerable<IDemoSeedContributor> contributors,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService,
        ITenantContext tenantContext,
        FinanceDbContext financeDbContext,
        IPersonalFinanceDemoDataReverser personalFinanceDemoDataReverser,
        IAgentDemoCleanup agentDemoCleanup)
        : this(
            dbContext,
            contributors,
            clock,
            loggerFactory,
            auditLogWriter,
            currentUserProvider,
            correlationContext,
            permissionService,
            tenantContext,
            financeDbContext,
            agentDemoCleanup,
            new IdentityRoleSeedPhase(dbContext, clock, currentUserProvider),
            new PartySeedPhase(dbContext, clock, currentUserProvider),
            new CrossBorderTenantSeedPhase(dbContext, clock, currentUserProvider),
            new SeedMarkerPhase(dbContext, clock, currentUserProvider, contributors),
            new ReverseSeedPhase(dbContext, financeDbContext, personalFinanceDemoDataReverser, agentDemoCleanup))
    {
    }

    public async Task<DemoSeedResult> SeedAsync(Guid tenantId, string? seedType = null, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);

        var normalizedSeedType = NormalizeSeedType(seedType);

        // Spec 065 — resolve the tenant's business type so the sample layer is keyed by it. The config
        // layer is applied from the config pack at provision; the demo seed adds sample CONTENT only.
        var tenantBusinessType = await _dbContext.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.BusinessType)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantBusinessType is null)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        var tenantSeedLock = TenantSeedLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await tenantSeedLock.WaitAsync(cancellationToken);

        try
        {
            _tenantContext.TenantId = tenantId;
            _tenantContext.ResolutionSource = "AdminTenantAction";

            var operations = new List<string>();
            var seedContext = new DemoSeedContext(tenantId, normalizedSeedType, _clock.UtcNow, _currentUserProvider.GetCurrentUserId(), tenantBusinessType);

            // Phase 1: Identity seed
            var identitySeed = new IdentitySeedService(_dbContext, _loggerFactory.CreateLogger<IdentitySeedService>());
            await identitySeed.SeedAsync(cancellationToken);
            operations.Add("IdentitySeed");
            ClearTrackingIfSupported(_dbContext);

            // Phase 2: Catalog reference data (Platform-only)
            var catalogSeed = new CatalogSeedService(_dbContext, _loggerFactory.CreateLogger<CatalogSeedService>());
            await catalogSeed.SeedAsync(cancellationToken);
            operations.Add("CatalogSeed");
            ClearTrackingIfSupported(_dbContext);

            // Phase 3: Module catalog categories (biller categories via contributors)
            await SeedContributorsAsync(DemoSeedPhase.CatalogCategories, seedContext, operations, cancellationToken);
            ClearContributorTracking();

            // Phase 4: Tenant admin role
            await _identityRolePhase.EnsureTenantAdminRoleAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 5: Bill collection partner (via contributors)
            await SeedContributorsAsync(DemoSeedPhase.BillCollectionPartner, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            // Phase 6: Demo catalog (via contributors)
            await SeedContributorsAsync(DemoSeedPhase.Catalog, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 7: Parties (Platform-only)
            var partyIds = await _partyPhase.SeedPartiesAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 7.5: Personal-finance personas — Seamus + Mark Keane.
            // Always runs (independent of seedType) so the Finance.Activity
            // phase below can attach a year of PF data to a stable pair of
            // UK parties. See PartySeedPhase.SeedPersonalFinancePersonasAsync.
            await _partyPhase.SeedPersonalFinancePersonasAsync(tenantId, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            // Phase 8: Pricing (via contributors)
            await SeedContributorsAsync(DemoSeedPhase.Pricing, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            // Phase 8.5: Workflows (Agents module)
            await SeedContributorsAsync(DemoSeedPhase.Workflows, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            // Phase 9: Seed marker
            await _markerPhase.UpsertMarkerAsync(tenantId, partyIds, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);

            if (normalizedSeedType == DemoSeedTypes.CrossBorderPayments)
            {
                var tenantSnapshot = await _markerPhase.CaptureTenantSnapshotAsync(tenantId, cancellationToken);

                // Phase 10: UK home base
                await _crossBorderTenantPhase.EnsureUkHomeBaseAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 11: Tenant coverage
                var tenantCoverage = await _crossBorderTenantPhase.SeedCrossBorderTenantCoverageAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 12: Cross-border partner network (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.CrossBorderPartnerNetwork, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                ClearContributorTracking();

                // Phase 13: Cross-border catalog (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.CrossBorderCatalog, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 14: Cross-border parties (Platform-only)
                var crossBorderParties = await _partyPhase.SeedCrossBorderPartiesAsync(tenantId, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);

                // Phase 15: Households (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.Households, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                ClearContributorTracking();

                // Phase 16: Cross-border pricing (via contributors)
                await SeedContributorsAsync(DemoSeedPhase.CrossBorderPricing, seedContext, operations, cancellationToken);
                ClearTrackingIfSupported(_dbContext);
                ClearContributorTracking();

                // Phase 17: Cross-border seed marker
                await _markerPhase.UpsertCrossBorderMarkerAsync(
                    tenantId,
                    normalizedSeedType,
                    tenantSnapshot,
                    partyIds,
                    tenantCoverage,
                    crossBorderParties,
                    operations,
                    cancellationToken);
            }

            // Phase 18: Activity
            await SeedContributorsAsync(DemoSeedPhase.Activity, seedContext, operations, cancellationToken);
            ClearTrackingIfSupported(_dbContext);
            ClearContributorTracking();

            var now = _clock.UtcNow;
            var userId = _currentUserProvider.GetCurrentUserId();

            await _auditLogWriter.LogAsync(
                AuditEventNames.TenantDemoSeeded,
                "TenantDemoSeed",
                tenantId,
                tenantId,
                userId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { tenantId, seedType = normalizedSeedType, operations }),
                cancellationToken);

            return new DemoSeedResult(tenantId, normalizedSeedType, now, operations);
        }
        finally
        {
            tenantSeedLock.Release();
        }
    }

    public async Task<DemoSeedResult> ReverseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);

        var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }

        var tenantSeedLock = TenantSeedLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await tenantSeedLock.WaitAsync(cancellationToken);

        try
        {
            _tenantContext.TenantId = tenantId;
            _tenantContext.ResolutionSource = "AdminTenantAction";

            var crossBorderSetting = await _markerPhase.GetSettingAsync(tenantId, CrossBorderDemoSeedKey, cancellationToken);
            var billCollectionSetting = await _markerPhase.GetSettingAsync(tenantId, DemoSeedKey, cancellationToken);
            var seedType = crossBorderSetting != null
                ? DemoSeedTypes.CrossBorderPayments
                : DemoSeedTypes.BillCollection;

            var operations = new List<string>();

            await _reversePhase.ReverseAgentActivityAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReverseNotificationsAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReverseOrdersAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReversePersonalFinanceActivityAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReverseHouseholdsAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReverseWorkflowRegistryAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReverseCatalogAndPricingAsync(tenantId, operations, cancellationToken);
            await _reversePhase.ReversePartnerNetworkAsync(tenantId, operations, cancellationToken);
            await _partyPhase.ReversePartiesAsync(tenantId, operations, cancellationToken);
            await _crossBorderTenantPhase.ReverseTenantCoverageAsync(tenantId, operations, cancellationToken);
            await _markerPhase.RestoreTenantProfileAsync(tenantId, crossBorderSetting, operations, cancellationToken);
            await _markerPhase.RemoveSeedMarkersAsync(tenantId, billCollectionSetting, crossBorderSetting, operations, cancellationToken);

            var now = _clock.UtcNow;
            var userId = _currentUserProvider.GetCurrentUserId();

            await _auditLogWriter.LogAsync(
                AuditEventNames.TenantDemoReversed,
                "TenantDemoSeed",
                tenantId,
                tenantId,
                userId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { tenantId, seedType, operations }),
                cancellationToken);

            return new DemoSeedResult(tenantId, seedType, now, operations);
        }
        finally
        {
            tenantSeedLock.Release();
        }
    }

    private async Task SeedContributorsAsync(DemoSeedPhase phase, DemoSeedContext context, List<string> operations, CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors)
        {
            var ops = await contributor.SeedAsync(phase, context, cancellationToken);
            operations.AddRange(ops);
        }
    }

    private void ClearContributorTracking()
    {
        foreach (var contributor in _contributors)
            contributor.ClearTracking();
    }

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
            throw new InvalidOperationException("Authenticated user is required.");

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
            throw new PermissionDeniedException(permissionKey);
    }

    private static string NormalizeSeedType(string? seedType)
    {
        if (string.IsNullOrWhiteSpace(seedType)) return DemoSeedTypes.BillCollection;
        if (string.Equals(seedType, DemoSeedTypes.BillCollection, StringComparison.OrdinalIgnoreCase)) return DemoSeedTypes.BillCollection;
        if (string.Equals(seedType, DemoSeedTypes.CrossBorderPayments, StringComparison.OrdinalIgnoreCase)) return DemoSeedTypes.CrossBorderPayments;
        throw new InvalidOperationException($"Unsupported demo seed type '{seedType}'.");
    }

    private static void ClearTrackingIfSupported(PlatformDbContext dbContext) => dbContext.ChangeTracker.Clear();
}
