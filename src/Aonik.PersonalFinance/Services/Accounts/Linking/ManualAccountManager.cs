using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services.Accounts.Linking;

/// <summary>
/// Manual (non-provider) account and transaction CRUD: lets users record
/// accounts and one-off transactions without going through an aggregator.
/// </summary>
internal sealed class ManualAccountManager
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPartyAccountService _partyAccountService;
    private readonly AccountConnectionSyncApplicator _syncApplicator;

    public ManualAccountManager(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        IPartyAccountService partyAccountService,
        AccountConnectionSyncApplicator syncApplicator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _partyAccountService = partyAccountService;
        _syncApplicator = syncApplicator;
    }

    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        AccountLinkingNormalization.ValidateRequiredText(request.Name, nameof(request.Name));
        AccountLinkingNormalization.ValidateRequiredText(request.AccountType, nameof(request.AccountType));
        AccountLinkingNormalization.ValidateRequiredText(request.Currency, nameof(request.Currency));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var tenantPartyId = await _syncApplicator.ResolveTenantPartyIdAsync(tenantId, cancellationToken);

        var maskedIdentifier = AccountLinkingNormalization.NormalizeLast4(request.Last4) ?? request.Name.Trim();
        var metadataJson = AccountLinkingNormalization.BuildAccountMetadataJson(request);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim().ToUpperInvariant();
        var country = AccountLinkingNormalization.TrimNullable(request.Country);

        var result = await _partyAccountService.CreatePartyAccountAsync(
            tenantId,
            tenantPartyId,
            request.AccountType.Trim(),
            maskedIdentifier,
            null,
            "Manual",
            currency,
            country,
            metadataJson,
            cancellationToken);

        return AccountConnectionResponseMapper.MapAccount(result);
    }

    public async Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var results = await _partyAccountService.ListPartyAccountsAsync(tenantId, cancellationToken);

        return results.Select(AccountConnectionResponseMapper.MapAccount).ToList();
    }

    public async Task<AccountTransactionResponse> CreateTransactionAsync(
        CreateAccountTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount == 0)
        {
            throw new ArgumentException("Amount cannot be zero.", nameof(request.Amount));
        }

        AccountLinkingNormalization.ValidateRequiredText(request.Currency, nameof(request.Currency));

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Verify account exists for this tenant
        var existingAccount = await _partyAccountService.GetPartyAccountAsync(
            tenantId,
            request.AccountId,
            cancellationToken);

        if (existingAccount == null)
        {
            throw new InvalidOperationException($"Account {request.AccountId} not found.");
        }

        var transaction = new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = request.AccountId,
            AccountConnectionId = null,
            ProviderTransactionReference = $"manual-{Guid.NewGuid():N}",
            OccurredAt = request.OccurredAt,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Counterparty = AccountLinkingNormalization.TrimNullable(request.Counterparty),
            Description = AccountLinkingNormalization.TrimNullable(request.Description),
            Reference = AccountLinkingNormalization.TrimNullable(request.Reference),
            Category = AccountLinkingNormalization.TrimNullable(request.Category),
            Pending = false,
            ReconciliationStatus = "Unmatched",
            Notes = AccountLinkingNormalization.TrimNullable(request.Notes)
        };

        _financeDbContext.AccountTransactions.Add(transaction);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return AccountConnectionResponseMapper.MapTransaction(transaction);
    }

    public async Task<PagedResult<AccountTransactionResponse>> ListTransactionsAsync(
        ListAccountTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _financeDbContext.AccountTransactions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (request.AccountId.HasValue)
        {
            query = query.Where(item => item.AccountId == request.AccountId.Value);
        }

        if (request.ConnectionId.HasValue)
        {
            query = query.Where(item => item.AccountConnectionId == request.ConnectionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ReconciliationStatus))
        {
            query = query.Where(item => item.ReconciliationStatus == request.ReconciliationStatus.Trim());
        }

        if (request.From.HasValue)
        {
            query = query.Where(item => item.OccurredAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(item => item.OccurredAt <= request.To.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(item => item.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AccountTransactionResponse(
                item.Id,
                item.AccountId,
                item.AccountConnectionId,
                item.OccurredAt,
                item.Amount,
                item.Currency,
                item.Counterparty,
                item.Description,
                item.Reference,
                item.Category,
                item.SubCategory,
                item.CategoryMethod,
                item.CategoryConfidence,
                item.CategoryLockedAt,
                item.Pending,
                item.ReconciliationStatus,
                item.MatchedLedgerEntryId,
                item.MatchedPayoutId,
                item.ReconciledAt,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountTransactionResponse>(items, totalCount, pageNumber, pageSize);
    }
}
