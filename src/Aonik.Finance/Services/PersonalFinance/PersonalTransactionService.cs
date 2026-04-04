using System.Text.Json;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalTransactionService : IPersonalTransactionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public PersonalTransactionService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PersonalTransactionResponse> CreateManualTransactionAsync(
        CreateManualPersonalTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateManualTransactionRequest(request.Amount, request.Currency, request.OccurredAt);

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var account = await GetOwnedAccountAsync(request.PersonalAccountId, userId, tenantId, cancellationToken);
        EnsureTransactionCurrencyMatchesAccount(request.Currency, account);

        var tags = NormalizeTags(request.Tags);

        var transaction = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            PersonalAccountId = request.PersonalAccountId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = request.OccurredAt,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Merchant = TrimNullable(request.Merchant),
            Description = TrimNullable(request.Description),
            Category = TrimNullable(request.Category),
            Notes = TrimNullable(request.Notes),
            TagsJson = JsonSerializer.Serialize(tags, JsonOptions),
            Confidence = 0,
            CategorisedBy = null,
            ClassificationMethod = null,
            ReviewStatus = "Pending"
        };

        ApplyCategorisation(transaction);

        _financeDbContext.PersonalTransactions.Add(transaction);

        if (account != null)
        {
            await ApplyManualAccountBalanceDeltaAsync(account, tenantId, userId, transaction.Amount, cancellationToken);
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return MapToResponse(transaction, tags);
    }

    public async Task<IReadOnlyList<PersonalTransactionResponse>> ListTransactionsAsync(
        ListPersonalTransactionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);

        var query = _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(transaction => transaction.TenantId == tenantId && transaction.UserId == userId);

        if (request.PersonalAccountId.HasValue)
        {
            query = query.Where(transaction => transaction.PersonalAccountId == request.PersonalAccountId.Value);
        }

        if (request.FinancialContextId.HasValue)
        {
            query = query.Where(transaction => transaction.FinancialContextId == request.FinancialContextId.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(transaction => transaction.OccurredAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(transaction => transaction.OccurredAt <= request.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(transaction => transaction.Category != null && transaction.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(transaction =>
                (transaction.Merchant != null && transaction.Merchant.Contains(search)) ||
                (transaction.Description != null && transaction.Description.Contains(search)) ||
                (transaction.Notes != null && transaction.Notes.Contains(search)));
        }

        var transactions = await query
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return transactions
            .Select(transaction => MapToResponse(transaction, DeserializeTags(transaction.TagsJson)))
            .ToList();
    }

    public async Task<PersonalTransactionResponse?> GetTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var transaction = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == transactionId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken);

        return transaction == null
            ? null
            : MapToResponse(transaction, DeserializeTags(transaction.TagsJson));
    }

    public async Task<PersonalTransactionResponse> UpdateManualTransactionAsync(
        Guid transactionId,
        UpdateManualPersonalTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateManualTransactionRequest(request.Amount, request.Currency, request.OccurredAt);

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var account = await GetOwnedAccountAsync(request.PersonalAccountId, userId, tenantId, cancellationToken);
        EnsureTransactionCurrencyMatchesAccount(request.Currency, account);

        var transaction = await _financeDbContext.PersonalTransactions
            .FirstOrDefaultAsync(
                item => item.Id == transactionId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Personal transaction not found.");

        var originalAccount = await GetOwnedAccountAsync(transaction.PersonalAccountId, userId, tenantId, cancellationToken);
        var originalAmount = transaction.Amount;

        var tags = NormalizeTags(request.Tags);

        transaction.PersonalAccountId = request.PersonalAccountId;
        transaction.OccurredAt = request.OccurredAt;
        transaction.Amount = request.Amount;
        transaction.Currency = request.Currency.Trim().ToUpperInvariant();
        transaction.Merchant = TrimNullable(request.Merchant);
        transaction.Description = TrimNullable(request.Description);
        transaction.Category = TrimNullable(request.Category);
        transaction.Notes = TrimNullable(request.Notes);
        transaction.TagsJson = JsonSerializer.Serialize(tags, JsonOptions);

        ApplyCategorisation(transaction);

        if (originalAccount?.Id == account?.Id)
        {
            if (account != null)
            {
                await ApplyManualAccountBalanceDeltaAsync(
                    account,
                    tenantId,
                    userId,
                    transaction.Amount - originalAmount,
                    cancellationToken);
            }
        }
        else
        {
            if (originalAccount != null)
            {
                await ApplyManualAccountBalanceDeltaAsync(
                    originalAccount,
                    tenantId,
                    userId,
                    -originalAmount,
                    cancellationToken);
            }

            if (account != null)
            {
                await ApplyManualAccountBalanceDeltaAsync(
                    account,
                    tenantId,
                    userId,
                    transaction.Amount,
                    cancellationToken);
            }
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return MapToResponse(transaction, tags);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task<PersonalAccount?> GetOwnedAccountAsync(
        Guid? personalAccountId,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!personalAccountId.HasValue)
        {
            return null;
        }

        var account = await _financeDbContext.PersonalAccounts
            .FirstOrDefaultAsync(
                account => account.Id == personalAccountId.Value
                    && account.TenantId == tenantId
                    && account.UserId == userId
                    && !account.IsArchived,
                cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException("Personal account not found or unavailable.");
        }

        return account;
    }

    private async Task ApplyManualAccountBalanceDeltaAsync(
        PersonalAccount account,
        Guid tenantId,
        Guid userId,
        decimal amountDelta,
        CancellationToken cancellationToken)
    {
        if (amountDelta == 0m)
        {
            return;
        }

        var isLinkedAccount = await _financeDbContext.PersonalLinkedAccounts
            .AnyAsync(
                item => item.PersonalAccountId == account.Id
                    && item.TenantId == tenantId
                    && item.UserId == userId,
                cancellationToken);

        if (isLinkedAccount)
        {
            return;
        }

        account.CurrentBalance += amountDelta;
        account.BalanceAsOf = DateTime.UtcNow;
    }

    private static void EnsureTransactionCurrencyMatchesAccount(string? currency, PersonalAccount? account)
    {
        if (account == null || string.IsNullOrWhiteSpace(currency))
        {
            return;
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (!string.Equals(account.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Transaction currency must match the selected account currency.", nameof(currency));
        }
    }

    private void ApplyCategorisation(PersonalTransaction transaction)
    {
        if (!string.IsNullOrWhiteSpace(transaction.Category))
        {
            transaction.Confidence = 1.0m;
            transaction.CategorisedBy = "manual";
            transaction.ClassificationMethod = "manual";
            transaction.ReviewStatus = "Reviewed";
            transaction.ReviewedByUserId = transaction.UserId;
            transaction.ReviewedAt = DateTime.UtcNow;
        }
        else
        {
            transaction.Confidence = 0;
            transaction.CategorisedBy = null;
            transaction.ClassificationMethod = null;
            transaction.ReviewStatus = "Pending";
            transaction.ReviewedByUserId = null;
            transaction.ReviewedAt = null;
        }

        transaction.TransactionType = TransactionCategoryReference.ResolveTransactionType(
            transaction.Category, transaction.Amount);
    }

    private static PersonalTransactionResponse MapToResponse(
        PersonalTransaction transaction,
        IReadOnlyList<string> tags)
    {
        return new PersonalTransactionResponse(
            transaction.Id,
            transaction.UserId,
            transaction.PersonalAccountId,
            transaction.FinancialContextId,
            transaction.OccurredAt,
            transaction.Amount,
            transaction.Currency,
            transaction.TransactionType,
            transaction.Merchant,
            transaction.Description,
            transaction.Category,
            transaction.SubCategory,
            transaction.Confidence,
            transaction.CategorisedBy,
            transaction.ClassificationMethod,
            transaction.Notes,
            tags,
            transaction.CreatedAt,
            transaction.UpdatedAt);
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return Array.Empty<string>();
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return Array.Empty<string>();
        }

        var tags = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions);
        return tags ?? new List<string>();
    }

    private static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateManualTransactionRequest(decimal amount, string? currency, DateTime occurredAt)
    {
        if (amount == 0)
        {
            throw new ArgumentException("Amount cannot be zero.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        if (occurredAt == default)
        {
            throw new ArgumentException("OccurredAt is required.", nameof(occurredAt));
        }
    }
}
