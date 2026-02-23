using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Ledger;
using Aonik.Application.Services;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Domain.Ledger.Entities;
using Aonik.SharedKernel.Abstractions;
using LedgerEntity = Aonik.Domain.Ledger.Entities.Ledger;

namespace Aonik.Application.Services.Ledger;

public class LedgerService : AdminServiceBase, ILedgerService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public LedgerService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
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
            account.CreatedAt);
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

        return accounts
            .Select(account => new LedgerAccountResponse(
                account.Id,
                account.LedgerId,
                account.Name,
                account.Code,
                account.AccountType,
                ledgerCurrencyLookup.TryGetValue(account.LedgerId, out var currency) ? currency : string.Empty,
                account.CreatedAt))
            .ToList();
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
