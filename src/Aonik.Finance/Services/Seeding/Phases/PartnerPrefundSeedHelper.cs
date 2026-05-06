using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Shared helper for ensuring partner prefund ledger accounts and opening
/// journal entries. Used by both <see cref="BillCollectionPartnerSeedPhase"/>
/// and <see cref="CrossBorderPartnerNetworkSeedPhase"/>.
/// </summary>
internal sealed class PartnerPrefundSeedHelper
{
    private const string PrefundAccountRole = "PrefundAsset";
    private const string PartnerPrefundSeedSourceType = "PartnerPrefundSeed";

    private readonly FinanceDbContext _db;

    public PartnerPrefundSeedHelper(FinanceDbContext db)
    {
        _db = db;
    }

    public async Task EnsurePartnerPrefundAccountAsync(
        Guid tenantId,
        Guid partnerId,
        string partnerName,
        string currencyCode,
        decimal openingBalance,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);

        var fundingAccount = await _db.PartnerFundingAccounts
            .FirstOrDefaultAsync(account =>
                account.TenantId == tenantId &&
                account.PartnerId == partnerId &&
                account.Currency == normalizedCurrency &&
                account.AccountRole == PrefundAccountRole,
                cancellationToken);

        var accountCode = BuildPartnerPrefundAccountCode(partnerId, normalizedCurrency);
        var ledgerAccount = await _db.LedgerAccounts
            .FirstOrDefaultAsync(account => account.TenantId == tenantId && account.Code == accountCode, cancellationToken);

        if (ledgerAccount == null)
        {
            ledgerAccount = new LedgerAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Asset",
                Name = $"Due From Partner {partnerName} ({normalizedCurrency})",
                Code = accountCode,
                DimensionsJson = JsonSerializer.Serialize(new
                {
                    partnerId,
                    currency = normalizedCurrency,
                    accountRole = PrefundAccountRole
                }),
                CreatedAt = now,
                CreatedBy = userId
            };

            _db.LedgerAccounts.Add(ledgerAccount);
        }

        if (fundingAccount == null)
        {
            fundingAccount = new PartnerFundingAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerId = partnerId,
                LedgerAccountId = ledgerAccount.Id,
                Currency = normalizedCurrency,
                AccountRole = PrefundAccountRole,
                Status = "Active",
                CreatedAt = now,
                CreatedBy = userId
            };

            _db.PartnerFundingAccounts.Add(fundingAccount);
        }
        else
        {
            fundingAccount.LedgerAccountId = ledgerAccount.Id;
            fundingAccount.Currency = normalizedCurrency;
            fundingAccount.Status = "Active";
            fundingAccount.UpdatedAt = now;
            fundingAccount.UpdatedBy = userId;
        }

        await EnsurePartnerPrefundOpeningEntryAsync(
            tenantId,
            ledgerId,
            fundingAccount,
            openingBalance,
            now,
            userId,
            cancellationToken);
    }

    private async Task EnsurePartnerPrefundOpeningEntryAsync(
        Guid tenantId,
        Guid ledgerId,
        PartnerFundingAccount fundingAccount,
        decimal openingBalance,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (openingBalance <= 0)
        {
            return;
        }

        var hasSeedEntry = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(entry =>
                entry.TenantId == tenantId &&
                entry.SourceType == PartnerPrefundSeedSourceType &&
                entry.SourceId == fundingAccount.Id,
                cancellationToken);

        if (hasSeedEntry)
        {
            return;
        }

        var cashAccountId = await ResolveCashLedgerAccountIdAsync(tenantId, cancellationToken);
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledgerId,
            Timestamp = now,
            SourceType = PartnerPrefundSeedSourceType,
            SourceId = fundingAccount.Id,
            Status = "Posted",
            CreatedAt = now,
            CreatedBy = userId,
            Lines = new List<JournalEntryLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerAccountId = fundingAccount.LedgerAccountId,
                    Direction = "Debit",
                    Amount = openingBalance,
                    Currency = fundingAccount.Currency,
                    Narration = "Seed prefund opening balance",
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId = fundingAccount.PartnerId,
                        fundingAccountId = fundingAccount.Id,
                        accountRole = fundingAccount.AccountRole
                    }),
                    CreatedAt = now,
                    CreatedBy = userId
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerAccountId = cashAccountId,
                    Direction = "Credit",
                    Amount = openingBalance,
                    Currency = fundingAccount.Currency,
                    Narration = "Seed prefund opening balance",
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId = fundingAccount.PartnerId,
                        fundingAccountId = fundingAccount.Id,
                        accountRole = fundingAccount.AccountRole
                    }),
                    CreatedAt = now,
                    CreatedBy = userId
                }
            }
        };

        _db.JournalEntries.Add(entry);
    }

    private async Task<Guid> ResolveCashLedgerAccountIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cashAccountId = await _db.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Code == "1000")
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (cashAccountId.HasValue)
        {
            return cashAccountId.Value;
        }

        cashAccountId = await _db.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Name == "Cash")
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!cashAccountId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a cash ledger account for prefund seeding.");
        }

        return cashAccountId.Value;
    }

    private async Task<Guid> GetTenantLedgerIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var ledgerId = await _db.Ledgers
            .AsNoTracking()
            .Where(ledger => ledger.TenantId == tenantId)
            .Select(ledger => (Guid?)ledger.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ledgerId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a ledger.");
        }

        return ledgerId.Value;
    }

    private static string BuildPartnerPrefundAccountCode(Guid partnerId, string currencyCode)
    {
        var partnerCode = partnerId.ToString("N")[..12].ToUpperInvariant();
        return $"1300-{partnerCode}-{currencyCode}";
    }
}
