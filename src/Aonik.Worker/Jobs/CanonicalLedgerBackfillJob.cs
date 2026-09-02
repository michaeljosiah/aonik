using Aonik.Finance.Persistence;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Spec 088 §10 — marks each tenant's canonical ledger so <c>ILedgerResolver</c> can answer.
///
/// A Worker job rather than SQL inside the migration: hand-authoring
/// <c>migrationBuilder.Sql(...)</c> is prohibited by <c>CLAUDE.md</c>, and a data backfill wants
/// things a migration body cannot give it — re-runnability, a report of what it touched, and the
/// ability to refuse rather than guess.
///
/// Idempotent: a tenant that already has a canonical ledger is skipped, so re-running is safe and
/// is the intended way to pick up tenants provisioned after the first run.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CanonicalLedgerBackfillJob : IJob
{
    public static readonly JobKey Key = new("CanonicalLedgerBackfillJob", ScheduledJobGroups.ScheduledJobs);

    private readonly FinanceDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CanonicalLedgerBackfillJob> _logger;
    private readonly IModuleEnablementReader? _moduleReader;

    public CanonicalLedgerBackfillJob(
        FinanceDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<CanonicalLedgerBackfillJob> logger,
        IModuleEnablementReader? moduleReader = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
        _moduleReader = moduleReader;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        // Backfill spans every tenant by design, so it must see across the tenant filter.
        // !IsDeleted is explicit: AcrossTenants disables the soft-delete filter too, and
        // ILedgerResolver reads through the normal filtered query. A deleted canonical ledger would
        // make this report a tenant as configured while its live ledgers stayed unmarked — and a
        // deleted sole ledger could itself be marked canonical. Both leave the tenant unable to post.
        var ledgers = await _dbContext.Ledgers
            .AcrossTenants()
            .Where(l => !l.IsDeleted)
            .Select(l => new { l.Id, l.TenantId, l.IsCanonical })
            .ToListAsync(ct);

        var byTenant = ledgers.GroupBy(l => l.TenantId).ToList();

        // Spec 097 §12.2: a tenant with Finance off cannot post, so nothing here is marked for it.
        var gate = await ModuleGatedTenants.FilterAsync(
            _moduleReader, byTenant.Select(t => t.Key).ToList(), ModuleIds.Finance, "Canonical ledger backfill", _logger, ct);
        var enabledTenants = gate.Enabled.ToHashSet();
        byTenant = byTenant.Where(t => enabledTenants.Contains(t.Key)).ToList();

        var marked = 0;
        var alreadySet = 0;
        var ambiguous = new List<Guid>();

        foreach (var tenant in byTenant)
        {
            if (tenant.Any(l => l.IsCanonical))
            {
                alreadySet++;
                continue;
            }

            var candidates = tenant.ToList();

            if (candidates.Count > 1)
            {
                // Deliberately NOT resolved here. Which of several ledgers is canonical is an
                // operator's decision about where money is recorded; a script picking the oldest
                // or the first would be inventing an answer with financial consequences.
                ambiguous.Add(tenant.Key);
                continue;
            }

            var only = await _dbContext.Ledgers
                .AcrossTenants()
                .FirstAsync(l => l.Id == candidates[0].Id && !l.IsDeleted, ct);

            // Stamp the ambient tenant and commit per tenant. The base context refuses to save a
            // tenant-scoped row whose TenantId differs from the ambient tenant, so a job that read
            // across tenants and saved once would throw on the first row it touched.
            _tenantContext.TenantId = tenant.Key;
            _tenantContext.ResolutionSource = "backfill";

            try
            {
                only.IsCanonical = true;
                await _dbContext.SaveChangesAsync(ct);
                marked++;
            }
            finally
            {
                _tenantContext.TenantId = null;
                _tenantContext.ResolutionSource = null;
            }
        }

        _logger.LogInformation(
            "Canonical ledger backfill: {Marked} marked, {AlreadySet} already set, {Ambiguous} ambiguous, {Tenants} tenants scanned.",
            marked, alreadySet, ambiguous.Count, byTenant.Count);

        context.Result = $"Marked {marked}, already set {alreadySet}, ambiguous {ambiguous.Count}." + gate.Note;

        if (ambiguous.Count > 0)
        {
            // Loud, not silent. A tenant left unmarked cannot post through ILedgerResolver at all,
            // and discovering that at the first payment is far worse than discovering it here.
            _logger.LogError(
                "Canonical ledger backfill left {Count} tenant(s) unresolved — each holds several ledgers with none marked canonical, "
                + "and no default may be invented. Mark one per tenant, then re-run this job. Tenants: {TenantIds}",
                ambiguous.Count,
                string.Join(", ", ambiguous));

            throw new InvalidOperationException(
                $"{ambiguous.Count} tenant(s) hold several ledgers with none marked canonical. "
                + "An operator must choose; see the preceding log entry for the tenant ids.");
        }
    }
}
