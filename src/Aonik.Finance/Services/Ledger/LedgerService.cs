using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Models.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Persistence;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

namespace Aonik.Finance.Services.Ledger;

internal class LedgerService : FinanceServiceBase, ILedgerService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly Services.Observability.FinanceMetrics _metrics;

    public LedgerService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        Services.Observability.FinanceMetrics metrics)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _metrics = metrics;
    }

    public async Task<LedgerResponse> CreateLedgerAsync(CreateLedgerRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Write", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var ledger = new LedgerEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = request.BaseCurrency
        };

        _dbContext.Ledgers.Add(ledger);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LedgerResponse(ledger.Id, ledger.BaseCurrency, ledger.CreatedAt);
    }

    public async Task<IReadOnlyList<LedgerResponse>> ListLedgersAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var ledgers = await _dbContext.Ledgers
            .Where(ledger => ledger.TenantId == tenantId)
            .OrderByDescending(ledger => ledger.CreatedAt)
            .Select(ledger => new LedgerResponse(ledger.Id, ledger.BaseCurrency, ledger.CreatedAt))
            .ToListAsync(cancellationToken);

        return ledgers;
    }

    public async Task<LedgerAccountResponse> CreateAccountAsync(CreateLedgerAccountRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Write", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var ledger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(ledger => ledger.Id == request.LedgerId && ledger.TenantId == tenantId, cancellationToken);

        if (ledger == null)
        {
            throw new InvalidOperationException("Ledger not found for the current tenant.");
        }

        var account = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Code = request.Code,
            AccountType = request.AccountType,
            LedgerId = request.LedgerId,
            TenantId = tenantId,
            DimensionsJson = "{}"
        };

        _dbContext.LedgerAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LedgerAccountResponse(
            account.Id,
            account.LedgerId,
            account.Name,
            account.Code,
            account.AccountType,
            ledger.BaseCurrency,
            account.CreatedAt,
            // A freshly-created account has no posted lines yet.
            new List<LedgerAccountBalance>());
    }

    public async Task<IReadOnlyList<LedgerAccountResponse>> ListAccountsAsync(ListLedgerAccountsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var ledgerCurrencyLookup = await _dbContext.Ledgers
            .Where(ledger => ledger.TenantId == tenantId)
            .ToDictionaryAsync(ledger => ledger.Id, ledger => ledger.BaseCurrency, cancellationToken);

        var query = _dbContext.LedgerAccounts
            .Where(account => account.TenantId == tenantId);

        if (request.LedgerId.HasValue)
        {
            query = query.Where(account => account.LedgerId == request.LedgerId.Value);
        }

        var accounts = await query
            .OrderBy(account => account.Name)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(a => a.Id).ToList();

        // Group all journal-entry-line activity for the listed accounts,
        // bucketing by (account, currency, direction). Done in a single
        // GroupBy on the database so a tenant with thousands of accounts
        // doesn't fan out to N queries.
        var lineSums = accountIds.Count == 0
            ? new List<LineSumRow>()
            : await _dbContext.JournalEntryLines
                .AsNoTracking()
                .Where(line => line.TenantId == tenantId && accountIds.Contains(line.LedgerAccountId))
                .GroupBy(line => new { line.LedgerAccountId, line.Currency, line.Direction })
                .Select(g => new LineSumRow(
                    g.Key.LedgerAccountId,
                    g.Key.Currency,
                    g.Key.Direction,
                    g.Sum(x => x.Amount)))
                .ToListAsync(cancellationToken);

        var balancesByAccount = ComputeBalances(accounts, lineSums);

        return accounts
            .Select(account => new LedgerAccountResponse(
                account.Id,
                account.LedgerId,
                account.Name,
                account.Code,
                account.AccountType,
                ledgerCurrencyLookup.TryGetValue(account.LedgerId, out var currency) ? currency : string.Empty,
                account.CreatedAt,
                balancesByAccount.TryGetValue(account.Id, out var balances)
                    ? balances
                    : new List<LedgerAccountBalance>()))
            .ToList();
    }

    private sealed record LineSumRow(
        Guid LedgerAccountId,
        string Currency,
        string Direction,
        decimal Amount);

    private static Dictionary<Guid, IReadOnlyList<LedgerAccountBalance>> ComputeBalances(
        IReadOnlyList<Aonik.Finance.Entities.Ledger.LedgerAccount> accounts,
        IReadOnlyList<LineSumRow> lineSums)
    {
        var accountTypeById = accounts.ToDictionary(a => a.Id, a => a.AccountType);
        var result = new Dictionary<Guid, IReadOnlyList<LedgerAccountBalance>>();

        foreach (var perAccount in lineSums.GroupBy(r => r.LedgerAccountId))
        {
            var accountId = perAccount.Key;
            if (!accountTypeById.TryGetValue(accountId, out var accountType))
            {
                continue;
            }

            // Asset / Expense use a debit-positive normal balance; everything
            // else (Liability / Equity / Income) is credit-positive. Anything
            // unrecognised falls back to debit-positive so balances are still
            // intelligible — they just may show negative for the unusual side.
            var debitPositive = accountType is "Asset" or "Expense";

            var perCurrency = perAccount
                .GroupBy(r => r.Currency)
                .Select(currencyGroup =>
                {
                    decimal debit = 0m, credit = 0m;
                    foreach (var row in currencyGroup)
                    {
                        if (string.Equals(row.Direction, "Debit", StringComparison.OrdinalIgnoreCase))
                        {
                            debit += row.Amount;
                        }
                        else if (string.Equals(row.Direction, "Credit", StringComparison.OrdinalIgnoreCase))
                        {
                            credit += row.Amount;
                        }
                    }
                    var balance = debitPositive ? debit - credit : credit - debit;
                    return new LedgerAccountBalance(currencyGroup.Key, balance);
                })
                .Where(b => b.Balance != 0m)
                .OrderBy(b => b.Currency)
                .ToList();

            if (perCurrency.Count > 0)
            {
                result[accountId] = perCurrency;
            }
        }

        return result;
    }

    public async Task<JournalEntryResponse> AddJournalEntryAsync(AddJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Write", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var ledger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(entryLedger => entryLedger.Id == request.LedgerId && entryLedger.TenantId == tenantId, cancellationToken);

        if (ledger == null)
        {
            throw new InvalidOperationException("Ledger not found for the current tenant.");
        }

        if (request.Lines.Count < 2)
        {
            throw new InvalidOperationException("Journal entries must include at least two lines.");
        }

        var normalizedLines = request.Lines.Select(line => new
        {
            line.AccountId,
            Direction = line.Direction.Trim(),
            line.Amount,
            Currency = string.IsNullOrWhiteSpace(line.Currency) ? ledger.BaseCurrency : line.Currency,
            line.Narration
        }).ToList();

        var totalDebit = normalizedLines
            .Where(line => string.Equals(line.Direction, "Debit", StringComparison.OrdinalIgnoreCase))
            .Sum(line => line.Amount);
        var totalCredit = normalizedLines
            .Where(line => string.Equals(line.Direction, "Credit", StringComparison.OrdinalIgnoreCase))
            .Sum(line => line.Amount);

        if (totalDebit <= 0 || totalCredit <= 0 || totalDebit != totalCredit)
        {
            throw new InvalidOperationException("Journal entry debits and credits must balance.");
        }

        var accountIds = normalizedLines.Select(line => line.AccountId).Distinct().ToList();
        var accountLookup = await _dbContext.LedgerAccounts
            .Where(account => account.TenantId == tenantId && account.LedgerId == ledger.Id && accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        if (accountLookup.Count != accountIds.Count)
        {
            throw new InvalidOperationException("One or more ledger accounts were not found in the selected ledger.");
        }

        var entryId = Guid.NewGuid();
        var entry = new JournalEntry
        {
            Id = entryId,
            LedgerId = ledger.Id,
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow,
            SourceType = "Manual",
            SourceId = Guid.Empty,
            Status = "Posted",
            Lines = normalizedLines.Select(line => new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = entryId,
                LedgerAccountId = line.AccountId,
                Direction = line.Direction,
                Amount = line.Amount,
                Currency = line.Currency,
                Narration = line.Narration,
                DimensionsJson = "{}"
            }).ToList()
        };

        _dbContext.JournalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Per-tenant ledger-entry counter. Currency is taken from the
        // first line — journal entries are constrained to a single
        // currency so this is unambiguous.
        var entryCurrency = normalizedLines[0].Currency;
        _metrics.RecordLedgerEntryPosted(tenantId, entryCurrency);

        return new JournalEntryResponse(
            entry.Id,
            entry.LedgerId,
            entry.Timestamp,
            entry.Status,
            request.Reference,
            request.Description,
            entry.Lines.Select(line => new JournalEntryLineResponse(
                line.Id,
                line.LedgerAccountId,
                line.Direction,
                line.Amount,
                line.Currency,
                line.Narration)).ToList());
    }

    public async Task<IReadOnlyList<JournalEntryResponse>> ListJournalEntriesAsync(ListJournalEntriesRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.JournalEntries
            .Where(entry => entry.TenantId == tenantId);

        if (request.LedgerId.HasValue)
        {
            query = query.Where(entry => entry.LedgerId == request.LedgerId.Value);
        }

        var entries = await query
            .Include(entry => entry.Lines)
            .OrderByDescending(entry => entry.Timestamp)
            .ToListAsync(cancellationToken);

        return entries.Select(entry => new JournalEntryResponse(
            entry.Id,
            entry.LedgerId,
            entry.Timestamp,
            entry.Status,
            null,
            null,
            entry.Lines.Select(line => new JournalEntryLineResponse(
                line.Id,
                line.LedgerAccountId,
                line.Direction,
                line.Amount,
                line.Currency,
                line.Narration)).ToList())).ToList();
    }
}
