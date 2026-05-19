using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Finance-module implementation of <see cref="ICustomerDataExportProvider"/>.
/// Exports all personal-finance entities for the given user(s).
/// </summary>
internal class CustomerDataExportProvider : ICustomerDataExportProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly PersonalFinanceDbContext _db;

    public CustomerDataExportProvider(PersonalFinanceDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, List<JsonElement>>> ExportAsync(
        Guid tenantId,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, List<JsonElement>>();

        if (userIds.Count == 0)
            return data;

        // ── Personal profiles ──
        await Export(data, "PersonalProfile",
            _db.PersonalProfiles.AsNoTracking()
                .Where(p => p.TenantId == tenantId && userIds.Contains(p.UserId)),
            cancellationToken);

        // ── Households ──
        var householdMemberIds = await _db.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && userIds.Contains(m.UserId))
            .Select(m => m.HouseholdId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (householdMemberIds.Count > 0)
        {
            await Export(data, "Household",
                _db.Households.AsNoTracking()
                    .Where(h => h.TenantId == tenantId && householdMemberIds.Contains(h.Id)),
                cancellationToken);

            await Export(data, "HouseholdMember",
                _db.HouseholdMembers.AsNoTracking()
                    .Where(m => m.TenantId == tenantId && householdMemberIds.Contains(m.HouseholdId)),
                cancellationToken);
        }

        // ── Financial connections (redacted) ──
        var connections = await _db.FinancialConnections
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && userIds.Contains(c.UserId))
            .ToListAsync(cancellationToken);

        foreach (var conn in connections)
        {
            conn.SecretReference = string.Empty;
            conn.SyncCursor = null;
        }
        if (connections.Count > 0)
            data["FinancialConnection"] = Serialize(connections);

        // ── Personal accounts ──
        var accounts = await _db.PersonalAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && userIds.Contains(a.UserId))
            .ToListAsync(cancellationToken);

        // Redact external references
        foreach (var acct in accounts)
            acct.ExternalReference = null;
        if (accounts.Count > 0)
            data["PersonalAccount"] = Serialize(accounts);

        var accountIds = accounts.Select(a => a.Id).ToList();

        // ── Personal linked accounts (redacted) ──
        var linkedAccounts = await _db.PersonalLinkedAccounts
            .AsNoTracking()
            .Where(la => la.TenantId == tenantId && userIds.Contains(la.UserId))
            .ToListAsync(cancellationToken);

        foreach (var la in linkedAccounts)
            la.ProviderAccountReference = string.Empty;
        if (linkedAccounts.Count > 0)
            data["PersonalLinkedAccount"] = Serialize(linkedAccounts);

        // ── Financial contexts ──
        await Export(data, "FinancialContext",
            _db.FinancialContexts.AsNoTracking()
                .Where(fc => fc.TenantId == tenantId && userIds.Contains(fc.UserId)),
            cancellationToken);

        var contextIds = data.ContainsKey("FinancialContext")
            ? data["FinancialContext"]
                .Select(e => e.TryGetProperty("id", out var p) && p.TryGetGuid(out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList()
            : new List<Guid>();

        if (contextIds.Count > 0)
        {
            await Export(data, "FinancialContextFundingSource",
                _db.FinancialContextFundingSources.AsNoTracking()
                    .Where(fs => fs.TenantId == tenantId && contextIds.Contains(fs.FinancialContextId)),
                cancellationToken);
        }

        // ── Transactions ──
        await Export(data, "PersonalTransaction",
            _db.PersonalTransactions.AsNoTracking()
                .Where(t => t.TenantId == tenantId && userIds.Contains(t.UserId)),
            cancellationToken);

        // ── Transaction attachments ──
        await Export(data, "TransactionAttachment",
            _db.TransactionAttachments.AsNoTracking()
                .Where(a => a.TenantId == tenantId && userIds.Contains(a.UserId)),
            cancellationToken);

        // ── Budgets + lines ──
        await Export(data, "Budget",
            _db.Budgets.AsNoTracking()
                .Where(b => b.TenantId == tenantId && userIds.Contains(b.UserId)),
            cancellationToken);

        var budgetIds = data.ContainsKey("Budget")
            ? data["Budget"]
                .Select(e => e.TryGetProperty("id", out var p) && p.TryGetGuid(out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList()
            : new List<Guid>();

        if (budgetIds.Count > 0)
        {
            await Export(data, "BudgetLine",
                _db.BudgetLines.AsNoTracking()
                    .Where(bl => bl.TenantId == tenantId && budgetIds.Contains(bl.BudgetId)),
                cancellationToken);
        }

        // ── Bills ──
        await Export(data, "Bill",
            _db.Bills.AsNoTracking()
                .Where(b => b.TenantId == tenantId && userIds.Contains(b.UserId)),
            cancellationToken);

        // ── Recurring bills ──
        await Export(data, "PersonalRecurringBill",
            _db.PersonalRecurringBills.AsNoTracking()
                .Where(r => r.TenantId == tenantId && userIds.Contains(r.UserId)),
            cancellationToken);

        // ── Subscriptions ──
        await Export(data, "Subscription",
            _db.Subscriptions.AsNoTracking()
                .Where(s => s.TenantId == tenantId && userIds.Contains(s.UserId)),
            cancellationToken);

        // ── Debt repayments ──
        await Export(data, "DebtRepayment",
            _db.DebtRepayments.AsNoTracking()
                .Where(d => d.TenantId == tenantId && userIds.Contains(d.UserId)),
            cancellationToken);

        // ── Goals ──
        await Export(data, "Goal",
            _db.Goals.AsNoTracking()
                .Where(g => g.TenantId == tenantId && userIds.Contains(g.UserId)),
            cancellationToken);

        // ── Categorisation rules (user-scoped) ──
        await Export(data, "CategorisationRule",
            _db.CategorisationRules.AsNoTracking()
                .Where(r => r.TenantId == tenantId && userIds.Contains(r.UserId)),
            cancellationToken);

        // ── Statement imports ──
        await Export(data, "StatementImport",
            _db.StatementImports.AsNoTracking()
                .Where(s => s.TenantId == tenantId && userIds.Contains(s.UserId)),
            cancellationToken);

        var importIds = data.ContainsKey("StatementImport")
            ? data["StatementImport"]
                .Select(e => e.TryGetProperty("id", out var p) && p.TryGetGuid(out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList()
            : new List<Guid>();

        if (importIds.Count > 0)
        {
            await Export(data, "StatementImportRow",
                _db.StatementImportRows.AsNoTracking()
                    .Where(r => r.StatementImportId != Guid.Empty && importIds.Contains(r.StatementImportId)),
                cancellationToken);
        }

        // ── Customer insight snapshots ──
        await Export(data, "CustomerInsightSnapshot",
            _db.CustomerInsightSnapshots.AsNoTracking()
                .Where(s => s.TenantId == tenantId && userIds.Contains(s.UserId)),
            cancellationToken);

        // ── Financial Life Graph ──
        await Export(data, "FinancialLifeGraphNode",
            _db.FinancialLifeGraphNodes.AsNoTracking()
                .Where(n => n.TenantId == tenantId && userIds.Contains(n.UserId)),
            cancellationToken);

        await Export(data, "FinancialLifeGraphEdge",
            _db.FinancialLifeGraphEdges.AsNoTracking()
                .Where(e => e.TenantId == tenantId && userIds.Contains(e.UserId)),
            cancellationToken);

        return data;
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static async Task Export<T>(
        Dictionary<string, List<JsonElement>> data,
        string key,
        IQueryable<T> query,
        CancellationToken cancellationToken)
    {
        var entities = await query.ToListAsync(cancellationToken);
        if (entities.Count > 0)
            data[key] = Serialize(entities);
    }

    private static List<JsonElement> Serialize<T>(IEnumerable<T> entities)
    {
        return entities
            .Select(e => JsonSerializer.SerializeToElement(e, JsonOptions))
            .ToList();
    }
}
