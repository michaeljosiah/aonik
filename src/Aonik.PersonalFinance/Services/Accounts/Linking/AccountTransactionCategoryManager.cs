using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Finance.Categorization;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Accounts.Linking;

/// <summary>
/// Manual category overrides, merchant-memory rule CRUD, and bulk
/// re-categorization for spec 028.
/// </summary>
internal sealed class AccountTransactionCategoryManager
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAccountTransactionCategorizer _categorizer;
    private readonly IChronicleCategoryMapper _categoryMapper;

    public AccountTransactionCategoryManager(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAccountTransactionCategorizer categorizer,
        IChronicleCategoryMapper categoryMapper)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _categorizer = categorizer;
        _categoryMapper = categoryMapper;
    }

    public async Task<AccountTransactionCategoryResult?> SetCategoryAsync(
        Guid transactionId,
        SetAccountTransactionCategoryRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCategoryOrThrow(request.Category, request.SubCategory);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var transaction = await _financeDbContext.AccountTransactions
            .FirstOrDefaultAsync(
                item => item.Id == transactionId && item.TenantId == tenantId,
                cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var category = request.Category.Trim();
        var subCategory = string.IsNullOrWhiteSpace(request.SubCategory)
            ? null
            : request.SubCategory.Trim();

        transaction.Category = category;
        transaction.SubCategory = subCategory;
        transaction.CategoryMethod = AccountTransactionCategorizer.MethodManual;
        transaction.CategoryConfidence = AccountTransactionCategorizer.ConfidenceManual;
        transaction.CategoryLockedAt = DateTime.UtcNow;

        var merchantRuleCreated = false;
        if (request.RememberForMerchant)
        {
            merchantRuleCreated = await UpsertMerchantRuleAsync(
                tenantId,
                transaction,
                category,
                subCategory,
                cancellationToken);
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return new AccountTransactionCategoryResult(
            transaction.Id,
            transaction.Category,
            transaction.SubCategory,
            transaction.CategoryMethod!,
            transaction.CategoryConfidence,
            transaction.CategoryLockedAt,
            merchantRuleCreated);
    }

    public async Task<bool> UnlockCategoryAsync(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var transaction = await _financeDbContext.AccountTransactions
            .FirstOrDefaultAsync(
                item => item.Id == transactionId && item.TenantId == tenantId,
                cancellationToken);

        if (transaction is null)
        {
            return false;
        }

        if (transaction.CategoryLockedAt is null)
        {
            return true;
        }

        transaction.CategoryLockedAt = null;
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<MerchantCategoryResult>> ListMerchantCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _financeDbContext.AccountTransactionMerchantCategories
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId)
            .OrderBy(rule => rule.MerchantKey)
            .Select(rule => new MerchantCategoryResult(
                rule.Id,
                rule.MerchantKey,
                rule.Category,
                rule.SubCategory,
                rule.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteMerchantCategoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var rule = await _financeDbContext.AccountTransactionMerchantCategories
            .FirstOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId,
                cancellationToken);

        if (rule is null)
        {
            return false;
        }

        _financeDbContext.AccountTransactionMerchantCategories.Remove(rule);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RecategorizeAccountTransactionsResult?> RecategorizeAsync(
        Guid connectionId,
        RecategorizeAccountTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var connectionExists = await _financeDbContext.AccountConnections
            .AnyAsync(
                item => item.Id == connectionId && item.TenantId == tenantId,
                cancellationToken);

        if (!connectionExists)
        {
            return null;
        }

        var query = _financeDbContext.AccountTransactions
            .Where(item => item.TenantId == tenantId
                && item.AccountConnectionId == connectionId);

        if (!request.IncludeLocked)
        {
            query = query.Where(item => item.CategoryLockedAt == null);
        }

        if (request.UnresolvedOnly)
        {
            // Unresolved == never categorised by the new pipeline, or
            // resolved to a fallback / "other" result that may improve
            // with a merchant rule.
            query = query.Where(item =>
                item.CategoryMethod == null
                || item.CategoryMethod == AccountTransactionCategorizer.MethodFallback
                || item.Category == null
                || item.Category == ChronicleCategoryCodes.Uncategorized
                || item.Category == ChronicleCategoryCodes.Other);
        }

        var transactions = await query.ToListAsync(cancellationToken);

        var merchantRules = await _financeDbContext.AccountTransactionMerchantCategories
            .Where(rule => rule.TenantId == tenantId)
            .ToDictionaryAsync(
                rule => rule.MerchantKey,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var processed = transactions.Count;
        var updated = 0;
        var skipped = 0;

        foreach (var transaction in transactions)
        {
            // When IncludeLocked is set, the caller has explicitly opted in to
            // losing the manual lock so the row can re-flow through the pipeline.
            if (request.IncludeLocked && transaction.CategoryLockedAt is not null)
            {
                transaction.CategoryLockedAt = null;
            }

            var beforeCategory = transaction.Category;
            var beforeSub = transaction.SubCategory;
            var beforeMethod = transaction.CategoryMethod;

            // Synthesise the provider result so the categorizer can re-run.
            // The old raw Plaid string (if any) lives in Category; detailed was
            // never persisted so we pass null.
            var syntheticProviderTx = new AccountLinkProviderTransactionResult(
                transaction.ProviderTransactionReference,
                string.Empty,
                transaction.OccurredAt,
                transaction.Amount,
                transaction.Currency,
                transaction.Counterparty,
                transaction.Description,
                transaction.Category,
                null,
                transaction.Pending);

            var merchantKey = MerchantKeyNormalizer.Normalize(transaction.Counterparty)
                ?? MerchantKeyNormalizer.Normalize(transaction.Description);
            AccountTransactionMerchantCategory? merchantRule = null;
            if (merchantKey is not null)
            {
                merchantRules.TryGetValue(merchantKey, out merchantRule);
            }

            _categorizer.Classify(transaction, syntheticProviderTx, merchantRule);

            var changed = transaction.Category != beforeCategory
                || transaction.SubCategory != beforeSub
                || transaction.CategoryMethod != beforeMethod;

            if (changed)
            {
                updated += 1;
            }
            else
            {
                skipped += 1;
            }
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return new RecategorizeAccountTransactionsResult(
            connectionId,
            processed,
            updated,
            skipped);
    }

    private async Task<bool> UpsertMerchantRuleAsync(
        Guid tenantId,
        AccountTransaction transaction,
        string category,
        string? subCategory,
        CancellationToken cancellationToken)
    {
        var merchantKey = MerchantKeyNormalizer.Normalize(transaction.Counterparty)
            ?? MerchantKeyNormalizer.Normalize(transaction.Description);

        if (merchantKey is null)
        {
            return false;
        }

        var existing = await _financeDbContext.AccountTransactionMerchantCategories
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.MerchantKey == merchantKey,
                cancellationToken);

        if (existing is null)
        {
            _financeDbContext.AccountTransactionMerchantCategories.Add(new AccountTransactionMerchantCategory
            {
                TenantId = tenantId,
                MerchantKey = merchantKey,
                Category = category,
                SubCategory = subCategory,
            });
            return true;
        }

        existing.Category = category;
        existing.SubCategory = subCategory;
        return false;
    }

    private void ValidateCategoryOrThrow(string category, string? subCategory)
    {
        if (!_categoryMapper.IsValidCategory(category))
        {
            throw new ArgumentException(
                $"'{category}' is not a recognised Chronicle category code.",
                nameof(category));
        }

        if (!string.IsNullOrWhiteSpace(subCategory)
            && !_categoryMapper.IsValidSubCategory(category, subCategory))
        {
            throw new ArgumentException(
                $"'{subCategory}' is not a valid sub-category for '{category}'.",
                nameof(subCategory));
        }
    }
}
