using System.Text.Json;

using Aonik.Platform.Contracts.Models.Modules;
using Aonik.Platform.Contracts.Services.Modules;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Platform.Services.Modules;

/// <summary>
/// Per-tenant module enablement (Spec 097 §7, §9): the cached read side behind
/// <see cref="IModuleEnablementReader"/> and the admin write side behind <see cref="ITenantModuleService"/>
/// on one scoped instance, so a write invalidates the very memo the gate and manifest read in the same
/// request. Row loading, resolution and invalidation are shared helpers for that reason.
/// </summary>
/// <remarks>
/// <para>
/// Reads are cached in FusionCache under <c>tenant-modules:{tenantId}</c> for 60 seconds with
/// fail-safe, and memoised per scope so the HTTP gate, the manifest and the agent resolver share one
/// lookup within a request. <see cref="InvalidateAsync"/> drops both; every write calls it and the
/// <see cref="TenantModulesChangedCacheInvalidator"/> calls it on the cross-host event.
/// </para>
/// <para>
/// <b>Propagation bound.</b> Invalidation is in-process (FusionCache is memory-only, no backplane),
/// so a toggle is immediate in the host that handled the PUT and in the Worker once the outbox
/// event is drained, but any OTHER API replica keeps serving the previous set for at most one
/// cache entry lifetime. That is why the duration is 60 seconds rather than longer: a disable
/// propagates across every replica within a minute (up to the fail-safe window of one hour only
/// if the store itself is unreadable). Closing the window entirely needs a FusionCache backplane
/// or distributed L2.
/// </para>
/// <para>
/// Every query here spans tenants on purpose: host admins read other tenants' state and Worker jobs run
/// with no ambient tenant, so the rows are filtered by the requested <c>TenantId</c> explicitly rather
/// than by the global filter. <c>AcrossTenants()</c> also drops the soft-delete filter, so deleted
/// rows are excluded explicitly.
/// </para>
/// <para>
/// <b>Write path shape.</b> A module that transitions from off to on in the RESOLVED set is provisioned
/// (its <see cref="ITenantProvisioningContributor"/>s run, dependencies first) BEFORE the toggle is
/// persisted, so a module is never reported enabled without the resources its endpoints assume — a
/// tenant whose pack disabled Finance at provisioning has no ledger until Finance is switched on here.
/// The toggle rows, the outbox message and the audit record then commit in ONE database transaction,
/// and the cache is invalidated only after that transaction succeeds. Contributors are resolved lazily
/// from the scope (never injected) because Finance's contributor depends on
/// <see cref="IModuleEnablementReader"/>, which is this very class.
/// </para>
/// </remarks>
internal sealed class TenantModuleService : IModuleEnablementReader, ITenantModuleService
{
    private const string AuditResourceType = "TenantModules";
    private const string CrossTenantReadPermission = "Tenants.Read";
    private const string AdminTenantActionSource = "AdminTenantAction";

    /// <summary>
    /// Audit action for an enablement that was rejected because a provisioning contributor threw. A
    /// distinct name so operators can query failed attempts apart from applied changes.
    /// </summary>
    internal const string ProvisioningFailedAuditAction = AuditEventNames.TenantModulesProvisioningFailed;

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly FusionCacheEntryOptions EntryOptions = new(TimeSpan.FromSeconds(60))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(1),
    };

    private readonly PlatformDbContext _dbContext;
    private readonly IFusionCache _cache;
    private readonly ILogger<TenantModuleService> _logger;
    private readonly Dictionary<Guid, ModuleEnablementSet> _scopeMemo = [];

    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IEventBus _eventBus;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissionService;
    private readonly IServiceProvider? _serviceProvider;

    /// <param name="serviceProvider">
    /// The scope to resolve <see cref="ITenantProvisioningContributor"/>s from when a module transitions
    /// on. Resolved lazily rather than injected: Finance's contributor takes
    /// <see cref="IModuleEnablementReader"/>, which is this class, so a constructor-injected
    /// <c>IEnumerable&lt;ITenantProvisioningContributor&gt;</c> would be a DI cycle. Optional so hosts and
    /// fixtures without the provisioning graph (read-only tools, the reader tests) still construct it;
    /// without one, no provisioning runs on enable.
    /// </param>
    public TenantModuleService(
        PlatformDbContext dbContext,
        IFusionCache cache,
        ILogger<TenantModuleService> logger,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IAuditLogWriter auditLogWriter,
        IEventBus eventBus,
        ITenantContext tenantContext,
        IPermissionService permissionService,
        IServiceProvider? serviceProvider = null)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _auditLogWriter = auditLogWriter;
        _eventBus = eventBus;
        _tenantContext = tenantContext;
        _permissionService = permissionService;
        _serviceProvider = serviceProvider;
    }

    internal static string CacheKey(Guid tenantId) => $"tenant-modules:{tenantId}";

    public async Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_scopeMemo.TryGetValue(tenantId, out var memoised))
            return memoised;

        var enabled = await _cache.GetOrSetAsync<IReadOnlySet<string>>(
            CacheKey(tenantId),
            async token => await ResolveFromStoreAsync(tenantId, token),
            EntryOptions,
            ct);

        var set = new ModuleEnablementSet(tenantId, enabled);
        _scopeMemo[tenantId] = set;
        return set;
    }

    public async Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
        IEnumerable<Guid> tenantIds,
        string moduleId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);

        if (!ModuleCatalog.IsKnown(moduleId))
            throw new ArgumentException($"'{moduleId}' is not a module in the catalogue.", nameof(moduleId));

        var requested = tenantIds.Distinct().ToList();
        if (requested.Count == 0)
            return [];

        // A core module is on for every tenant by definition — no round-trip needed.
        if (ModuleCatalog.CoreIds.Contains(moduleId))
            return requested;

        // One query for the whole list. EF Core translates Contains over a parameter list to
        // OPENJSON on SQL Server, so a large fan-out does not hit the parameter limit.
        var rows = await _dbContext.TenantModules
            .AsNoTracking()
            .AcrossTenants()
            .Where(row => !row.IsDeleted && requested.Contains(row.TenantId))
            .Select(row => new { row.TenantId, row.ModuleId, row.IsEnabled })
            .ToListAsync(ct);

        var rowsByTenant = rows
            .GroupBy(row => row.TenantId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, bool>)ToExplicitRows(
                    group.Select(row => (row.ModuleId, row.IsEnabled))));

        var result = new List<Guid>(requested.Count);
        foreach (var tenantId in requested)
        {
            var explicitRows = rowsByTenant.TryGetValue(tenantId, out var tenantRows)
                ? tenantRows
                : EmptyRows;

            if (ModuleCatalog.ResolveEnabled(explicitRows).Contains(moduleId))
                result.Add(tenantId);
        }

        return result;
    }

    /// <summary>
    /// Drops the cached and memoised set for <paramref name="tenantId"/>. Called after every write and
    /// on <see cref="SharedKernel.Events.Integration.TenantModulesChangedEvent"/>.
    /// </summary>
    public async Task InvalidateAsync(Guid tenantId, CancellationToken ct = default)
    {
        _scopeMemo.Remove(tenantId);
        await _cache.RemoveAsync(CacheKey(tenantId), token: ct);
        _logger.LogDebug("Invalidated module enablement cache for tenant {TenantId}.", tenantId);
    }

    // ── ITenantModuleService (Spec 097 §9) ──────────────────────────────────────────────────────

    Task<TenantModuleList> ITenantModuleService.GetAsync(Guid tenantId, CancellationToken cancellationToken)
        => GetListAsync(tenantId, cancellationToken);

    /// <summary>
    /// Every catalogue module with the tenant's state. The tenancy guard mirrors the feature
    /// endpoints: reading the ambient tenant needs nothing extra; reading any other tenant needs
    /// <c>Tenants.Read</c>.
    /// </summary>
    public async Task<TenantModuleList> GetListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null || _tenantContext.TenantId.Value != tenantId)
        {
            var userId = _currentUserProvider.GetCurrentUserId()
                ?? throw new PermissionDeniedException(CrossTenantReadPermission, "Authenticated user is required.");

            if (!await _permissionService.HasPermissionAsync(userId, CrossTenantReadPermission, cancellationToken))
                throw new PermissionDeniedException(CrossTenantReadPermission);
        }

        await EnsureTenantExistsAsync(tenantId, cancellationToken);

        var rows = await LoadRowsAsync(tenantId, cancellationToken);
        return Project(tenantId, rows);
    }

    public async Task<TenantModuleList> UpdateAsync(
        Guid tenantId,
        IReadOnlyList<TenantModuleToggle> toggles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toggles);

        // (a) Shape validation — defence in depth behind the endpoint validator.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var toggle in toggles)
        {
            if (!ModuleCatalog.IsKnown(toggle.ModuleId))
                throw new ArgumentException($"'{toggle.ModuleId}' is not a module in the catalogue.", nameof(toggles));

            if (ModuleCatalog.CoreIds.Contains(toggle.ModuleId))
                throw new ArgumentException($"Module '{toggle.ModuleId}' is core and cannot be toggled.", nameof(toggles));

            if (!seen.Add(toggle.ModuleId))
                throw new ArgumentException($"Module '{toggle.ModuleId}' appears more than once in the request.", nameof(toggles));
        }

        // The tenant row itself, not just its existence: provisioning contributors need its currency
        // and business type (the same context TenantProvisioner builds).
        var tenant = await _dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant {tenantId} not found");

        // Tracked rows: these are the ones we upsert below.
        var rows = await _dbContext.TenantModules
            .AcrossTenants()
            .Where(row => !row.IsDeleted && row.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var rowsById = new Dictionary<string, TenantModule>(StringComparer.Ordinal);
        foreach (var row in rows)
            rowsById[row.ModuleId] = row;

        var before = ModuleCatalog.ResolveEnabled(ToExplicitRows(rows.Select(row => (row.ModuleId, row.IsEnabled))));

        // (b) 'wanted' = catalogue defaults, overlaid with existing rows, overlaid with the request.
        //     Deliberately NOT dependency-closed: the checks below reject an inconsistent result
        //     rather than cascading silently.
        var wanted = ModuleCatalog.All
            .Where(descriptor => descriptor.DefaultEnabled || descriptor.IsCore)
            .Select(descriptor => descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows.Where(row => ModuleCatalog.IsKnown(row.ModuleId)))
        {
            if (row.IsEnabled) wanted.Add(row.ModuleId);
            else wanted.Remove(row.ModuleId);
        }

        foreach (var toggle in toggles)
        {
            if (toggle.IsEnabled) wanted.Add(toggle.ModuleId);
            else wanted.Remove(toggle.ModuleId);
        }

        wanted.UnionWith(ModuleCatalog.CoreIds);

        // (c) Enabling X needs X's whole hard chain on.
        foreach (var toggle in toggles.Where(toggle => toggle.IsEnabled))
        {
            var missing = ModuleCatalog.HardDependencyClosure([toggle.ModuleId])
                .Where(id => id != toggle.ModuleId && !wanted.Contains(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (missing.Count > 0)
                throw new ModuleDependencyException(ModuleErrorCodes.DependencyMissing, toggle.ModuleId, missing);
        }

        // (d) Disabling X is blocked while anything that hard-depends on X stays on. A dependent the
        //     same request disables is already out of 'wanted', so it does not count.
        foreach (var toggle in toggles.Where(toggle => !toggle.IsEnabled))
        {
            var dependents = ModuleCatalog.Dependents(toggle.ModuleId)
                .Where(wanted.Contains)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (dependents.Count > 0)
                throw new ModuleDependencyException(ModuleErrorCodes.DependentsEnabled, toggle.ModuleId, dependents);
        }

        // (e) The resolved effect, computed BEFORE anything is tracked or written: the off→on modules
        //     (dependency-closed, so a dependency the cascade switches on counts) are what provisioning
        //     runs for, and the tracker must stay clean until every contributor has succeeded — the
        //     failure audit below saves through this same context and must not carry the toggle with it.
        var afterRows = ToExplicitRows(rows.Select(row => (row.ModuleId, row.IsEnabled)));
        foreach (var toggle in toggles)
            afterRows[toggle.ModuleId] = toggle.IsEnabled;

        var after = ModuleCatalog.ResolveEnabled(afterRows);
        var enabledIds = after.Except(before).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var disabledIds = before.Except(after).OrderBy(id => id, StringComparer.Ordinal).ToList();

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        // (f) The ambient tenant becomes the target tenant for the rest of the call — the same
        //     "AdminTenantAction" switch the feature service performs. It has to happen BEFORE the
        //     contributors run, not just before our SaveChanges: they write through their own module
        //     contexts, whose tenant provider reads this context, and their "already provisioned?"
        //     checks run under the global tenant filter — under a host admin's own tenant, Finance
        //     would see no ledger and create a second one.
        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = AdminTenantActionSource;

        var committed = false;
        try
        {
            // (g) Provision every module that transitions on, dependencies first, before the toggle is
            //     persisted. A contributor failure surfaces as ModuleProvisioningException (audited) and
            //     leaves the tenant's module state exactly as it was.
            var provisioned = await ProvisionNewlyEnabledAsync(tenant, after, enabledIds, userId, now, cancellationToken);

            // (h) Upsert into the tracker.
            var changes = new List<object>(toggles.Count);
            foreach (var toggle in toggles)
            {
                var descriptor = ModuleCatalog.Get(toggle.ModuleId);
                if (rowsById.TryGetValue(toggle.ModuleId, out var existing))
                {
                    changes.Add(new
                    {
                        moduleId = toggle.ModuleId,
                        before = existing.IsEnabled,
                        after = toggle.IsEnabled,
                        previousSource = existing.Source,
                        reason = toggle.Reason,
                    });

                    existing.IsEnabled = toggle.IsEnabled;
                    existing.Source = TenantModuleSource.Explicit;
                    existing.Reason = toggle.Reason;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = userId;
                }
                else
                {
                    changes.Add(new
                    {
                        moduleId = toggle.ModuleId,
                        before = descriptor.DefaultEnabled,
                        after = toggle.IsEnabled,
                        previousSource = TenantModuleStateSource.Default,
                        reason = toggle.Reason,
                    });

                    var created = new TenantModule
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ModuleId = toggle.ModuleId,
                        IsEnabled = toggle.IsEnabled,
                        Source = TenantModuleSource.Explicit,
                        Reason = toggle.Reason,
                        CreatedAt = now,
                        CreatedBy = userId,
                    };

                    _dbContext.TenantModules.Add(created);
                    rowsById[toggle.ModuleId] = created;
                }
            }

            var changedEvent = new TenantModulesChangedEvent(tenantId, enabledIds, disabledIds, userId);

            // (i) Durable publication is staged on the same tracker as the rows; the Worker drains it.
            _dbContext.EnqueueIntegrationEvent(changedEvent);

            var auditPayload = JsonSerializer.Serialize(new
            {
                tenantId,
                changes,
                enabled = enabledIds,
                disabled = disabledIds,
                provisioned,
            }, AuditJsonOptions);

            // (j) Rows, outbox message and audit record commit as ONE unit. The audit writer saves through
            //     this same scoped context, so it is invoked while the rows and the outbox message are
            //     still pending: its SaveChanges flushes all three in one batch, and our own SaveChanges
            //     afterwards is a no-op. The explicit transaction (relational providers only — InMemory
            //     has none) covers a writer that saves any other way, and the execution strategy wrapper
            //     is what makes a user transaction legal under EnableRetryOnFailure.
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // PlatformDbContext enables EnableRetryOnFailure, so this delegate must be REPLAYABLE.
                // A rollback does not untrack: the audit row added by a failed attempt stays Added, and a
                // retry would call the writer again and commit TWO audit rows for one toggle. Drop any
                // audit entry a previous attempt left behind before writing this attempt's. The module
                // rows and the outbox message replay correctly as they are — same entities, same values.
                foreach (var stale in _dbContext.ChangeTracker.Entries<AuditLog>()
                             .Where(entry => entry.State == EntityState.Added)
                             .ToList())
                {
                    stale.State = EntityState.Detached;
                }

                await using var transaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                    : null;

                await _auditLogWriter.LogAsync(
                    AuditEventNames.TenantModulesUpdated,
                    AuditResourceType,
                    tenantId,
                    tenantId,
                    userId,
                    _correlationContext.CorrelationId,
                    auditPayload,
                    cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);

                committed = true;
            });

            // (k) In-process publication reaches this host's handlers now; the outbox path never
            //     reaches an API process (see TenantModulesChangedCacheInvalidator). The direct cache
            //     drop lives in the finally below so it happens for every committed change even if a
            //     handler throws here.
            await _eventBus.PublishAsync(changedEvent, cancellationToken);

            _logger.LogInformation(
                "Tenant {TenantId} modules updated by {UserId}: enabled [{Enabled}], disabled [{Disabled}].",
                tenantId,
                userId,
                string.Join(", ", enabledIds),
                string.Join(", ", disabledIds));
        }
        finally
        {
            // The scope memo may hold the post-toggle set pre-seeded for the contributors; it must not
            // outlive this call whatever happened. The shared cache is dropped ONLY for a committed
            // change — a rolled-back transaction changed nothing, so there is nothing to invalidate —
            // and with an uncancellable token: once the change is durable this host must stop
            // enforcing the previous state regardless of how the request ends.
            _scopeMemo.Remove(tenantId);
            if (committed)
                await InvalidateAsync(tenantId, CancellationToken.None);
        }

        // (l) Return the fresh, dependency-consistent projection.
        return Project(tenantId, await LoadRowsAsync(tenantId, cancellationToken));
    }

    /// <summary>
    /// Runs the provisioning contributors of every module in <paramref name="enabledIds"/> (the off→on
    /// transitions of the resolved set), hard dependencies first, with the same context
    /// <c>TenantProvisioner</c> builds. Contributors are idempotent by contract, so a module that was
    /// provisioned before it was switched off is a no-op here. A throwing contributor is audited as
    /// <see cref="ProvisioningFailedAuditAction"/> and surfaced as <see cref="ModuleProvisioningException"/>.
    /// </summary>
    /// <returns>The audit shape: one entry per contributor run with its reported actions.</returns>
    private async Task<List<object>> ProvisionNewlyEnabledAsync(
        Tenant tenant,
        IReadOnlySet<string> after,
        IReadOnlyList<string> enabledIds,
        Guid? userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var provisioned = new List<object>();
        if (enabledIds.Count == 0 || _serviceProvider is null)
            return provisioned;

        var contributors = _serviceProvider.GetServices<ITenantProvisioningContributor>().ToList();
        if (contributors.Count == 0)
            return provisioned;

        // Contributors that gate on the module set (Finance's ledger-account loop skips accounts for a
        // module that is off) must see the set this request is about to commit, not the one still in
        // the store — exactly what they would have seen at initial provisioning, where the pack's rows
        // land before the contributor loop. The memo is scope-local; UpdateAsync's finally drops it.
        _scopeMemo[tenant.Id] = new ModuleEnablementSet(tenant.Id, after);

        var context = new TenantProvisioningContext(tenant.Id, tenant.DefaultCurrency, userId, now, tenant.BusinessType);

        foreach (var moduleId in OrderByHardDependencies(enabledIds))
        {
            foreach (var contributor in contributors.Where(candidate => string.Equals(candidate.ModuleName, moduleId, StringComparison.Ordinal)))
            {
                var contributorName = contributor.GetType().Name;
                try
                {
                    var contribution = await contributor.ContributeProvisioningAsync(context, cancellationToken);
                    provisioned.Add(new { moduleId, contributor = contributorName, actions = contribution.ActionsPerformed });

                    _logger.LogInformation(
                        "Provisioned module {ModuleId} for tenant {TenantId} on enable via {Contributor}: {Actions}",
                        moduleId,
                        tenant.Id,
                        contributorName,
                        string.Join("; ", contribution.ActionsPerformed));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Provisioning module {ModuleId} for tenant {TenantId} on enable failed in {Contributor}; the toggle is not persisted.",
                        moduleId,
                        tenant.Id,
                        contributorName);

                    await RecordProvisioningFailureAsync(tenant.Id, moduleId, contributorName, enabledIds, provisioned, ex, userId, cancellationToken);
                    throw new ModuleProvisioningException(moduleId, contributorName, ex);
                }
            }
        }

        return provisioned;
    }

    /// <summary>
    /// Audits a rejected enablement. Nothing of the toggle is tracked at this point, so the writer's
    /// SaveChanges carries only the audit row. A failure to audit is logged, never allowed to mask the
    /// provisioning failure the caller must see.
    /// </summary>
    private async Task RecordProvisioningFailureAsync(
        Guid tenantId,
        string moduleId,
        string contributorName,
        IReadOnlyList<string> enabledIds,
        IReadOnlyList<object> provisioned,
        Exception error,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogWriter.LogAsync(
                ProvisioningFailedAuditAction,
                AuditResourceType,
                tenantId,
                tenantId,
                userId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new
                {
                    tenantId,
                    moduleId,
                    contributor = contributorName,
                    requested = enabledIds,
                    provisioned,
                    error = new { type = error.GetType().FullName, message = error.Message },
                }, AuditJsonOptions),
                cancellationToken);
        }
        catch (Exception auditFailure) when (auditFailure is not OperationCanceledException)
        {
            _logger.LogError(
                auditFailure,
                "Could not audit the failed enablement of module {ModuleId} for tenant {TenantId}.",
                moduleId,
                tenantId);
        }
    }

    /// <summary>
    /// Orders module ids so every hard dependency precedes its dependents. The size of a module's
    /// hard-dependency closure (which includes itself) is a valid topological key: a dependency's
    /// closure is a strict subset of its dependent's. Ties break on the id for determinism.
    /// </summary>
    internal static IReadOnlyList<string> OrderByHardDependencies(IEnumerable<string> moduleIds)
        => moduleIds
            .OrderBy(id => ModuleCatalog.HardDependencyClosure([id]).Count)
            .ThenBy(id => id, StringComparer.Ordinal)
            .ToList();

    /// <summary>Projects rows onto the full catalogue (Spec 097 §9): every module, with state and provenance.</summary>
    private static TenantModuleList Project(Guid tenantId, IReadOnlyList<TenantModule> rows)
    {
        var rowsById = new Dictionary<string, TenantModule>(StringComparer.Ordinal);
        foreach (var row in rows)
            rowsById[row.ModuleId] = row;

        var enabled = ModuleCatalog.ResolveEnabled(ToExplicitRows(rows.Select(row => (row.ModuleId, row.IsEnabled))));

        var states = ModuleCatalog.All
            .Select(descriptor =>
            {
                rowsById.TryGetValue(descriptor.Id, out var row);

                var source = descriptor.IsCore
                    ? TenantModuleStateSource.Core
                    : row?.Source ?? TenantModuleStateSource.Default;

                return new TenantModuleState(
                    descriptor.Id,
                    descriptor.Name,
                    descriptor.Description,
                    descriptor.IsCore,
                    descriptor.DependsOn,
                    descriptor.SoftDependsOn,
                    enabled.Contains(descriptor.Id),
                    source,
                    row?.Reason,
                    row?.UpdatedAt ?? row?.CreatedAt,
                    row?.UpdatedBy ?? row?.CreatedBy);
            })
            .ToList();

        return new TenantModuleList(tenantId, states);
    }

    private async Task EnsureTenantExistsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Tenants.AsNoTracking().AnyAsync(tenant => tenant.Id == tenantId, cancellationToken);
        if (!exists)
            throw new NotFoundException($"Tenant {tenantId} not found");
    }

    // ── Internal helpers (shared by the read and write sides) ───────────────────────────────────

    private static readonly IReadOnlyDictionary<string, bool> EmptyRows =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>Loads the tenant's live rows regardless of the ambient tenant.</summary>
    internal Task<List<TenantModule>> LoadRowsAsync(Guid tenantId, CancellationToken ct)
        => _dbContext.TenantModules
            .AsNoTracking()
            .AcrossTenants()
            .Where(row => !row.IsDeleted && row.TenantId == tenantId)
            .ToListAsync(ct);

    /// <summary>Maps rows to the explicit overlay that <see cref="ModuleCatalog.ResolveEnabled(IReadOnlyDictionary{string, bool})"/> consumes.</summary>
    internal static Dictionary<string, bool> ToExplicitRows(IEnumerable<(string ModuleId, bool IsEnabled)> rows)
    {
        var explicitRows = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (moduleId, isEnabled) in rows)
            explicitRows[moduleId] = isEnabled;
        return explicitRows;
    }

    private async Task<IReadOnlySet<string>> ResolveFromStoreAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await LoadRowsAsync(tenantId, ct);
        var explicitRows = ToExplicitRows(rows.Select(row => (row.ModuleId, row.IsEnabled)));
        return ModuleCatalog.ResolveEnabled(explicitRows);
    }
}
