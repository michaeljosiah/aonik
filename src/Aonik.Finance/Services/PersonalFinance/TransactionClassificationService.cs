using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class TransactionClassificationService : ITransactionClassificationService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public TransactionClassificationService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<CategorisationRuleResponse> CreateRuleAsync(
        CreateCategorisationRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Pattern, nameof(request.Pattern));
        ValidateRequiredText(request.Category, nameof(request.Category));
        ValidateRequiredText(request.MatchType, nameof(request.MatchType));
        ValidateRequiredText(request.Scope, nameof(request.Scope));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        await EnsureOwnedAccountAsync(request.AppliesToAccountId, userId, tenantId, cancellationToken);

        var rule = new CategorisationRule
        {
            TenantId = tenantId,
            UserId = userId,
            Pattern = request.Pattern.Trim(),
            Category = request.Category.Trim(),
            Priority = request.Priority,
            IsActive = true,
            MatchType = NormalizeMatchType(request.MatchType, nameof(request.MatchType)),
            CaseSensitive = request.CaseSensitive,
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            AppliesToAccountId = request.AppliesToAccountId,
            CreatedFromUserCorrection = false,
            Scope = request.Scope.Trim(),
            ApprovalStatus = "Approved"
        };

        _financeDbContext.CategorisationRules.Add(rule);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapRule(rule);
    }

    public async Task<IReadOnlyList<CategorisationRuleResponse>> ListRulesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var rules = await _financeDbContext.CategorisationRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId && rule.UserId == userId)
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Pattern)
            .ToListAsync(cancellationToken);

        return rules.Select(MapRule).ToList();
    }

    public async Task<CategorisationRuleResponse> UpdateRuleAsync(
        Guid ruleId,
        UpdateCategorisationRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Pattern, nameof(request.Pattern));
        ValidateRequiredText(request.Category, nameof(request.Category));
        ValidateRequiredText(request.MatchType, nameof(request.MatchType));
        ValidateRequiredText(request.Scope, nameof(request.Scope));
        ValidateRequiredText(request.ApprovalStatus, nameof(request.ApprovalStatus));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        await EnsureOwnedAccountAsync(request.AppliesToAccountId, userId, tenantId, cancellationToken);

        var rule = await _financeDbContext.CategorisationRules
            .FirstOrDefaultAsync(
                item => item.Id == ruleId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Categorisation rule not found.");

        rule.Pattern = request.Pattern.Trim();
        rule.Category = request.Category.Trim();
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.MatchType = NormalizeMatchType(request.MatchType, nameof(request.MatchType));
        rule.CaseSensitive = request.CaseSensitive;
        rule.MinAmount = request.MinAmount;
        rule.MaxAmount = request.MaxAmount;
        rule.AppliesToAccountId = request.AppliesToAccountId;
        rule.Scope = request.Scope.Trim();
        rule.ApprovalStatus = request.ApprovalStatus.Trim();

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapRule(rule);
    }

    public async Task DeactivateRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var rule = await _financeDbContext.CategorisationRules
            .FirstOrDefaultAsync(
                item => item.Id == ruleId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Categorisation rule not found.");

        rule.IsActive = false;
        await _financeDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassificationReviewItemResponse>> GetReviewQueueAsync(
        ClassificationReviewQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);

        var query = _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TenantId == tenantId
                && transaction.UserId == userId
                && (transaction.ReviewStatus == "Pending" || string.IsNullOrWhiteSpace(transaction.Category)));

        if (request.PersonalAccountId.HasValue)
        {
            query = query.Where(transaction => transaction.PersonalAccountId == request.PersonalAccountId.Value);
        }

        var transactions = await query
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return transactions.Select(MapReviewItem).ToList();
    }

    public async Task<ClassificationReviewItemResponse> AcceptClassificationAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedTransactionAsync(transactionId, cancellationToken)
            ?? throw new InvalidOperationException("Personal transaction not found.");

        var rules = await GetActiveRulesAsync(transaction, cancellationToken);
        ApplyBestRule(transaction, rules);

        transaction.ReviewStatus = "Reviewed";
        transaction.ReviewedByUserId = GetCurrentUserId();
        transaction.ReviewedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(transaction.Category))
        {
            transaction.Category = "Uncategorized";
            transaction.Confidence = 0;
            transaction.CategorisedBy = "manual";
            transaction.ClassificationMethod = "manual_fallback";
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        return MapReviewItem(transaction);
    }

    public async Task<ClassificationReviewItemResponse> OverrideClassificationAsync(
        Guid transactionId,
        OverrideTransactionClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Category, nameof(request.Category));

        var transaction = await GetOwnedTransactionAsync(transactionId, cancellationToken)
            ?? throw new InvalidOperationException("Personal transaction not found.");

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        transaction.Category = request.Category.Trim();
        transaction.Confidence = 1.0m;
        transaction.CategorisedBy = "manual";
        transaction.ClassificationMethod = "manual";
        transaction.ReviewStatus = "Reviewed";
        transaction.ReviewedAt = DateTime.UtcNow;
        transaction.ReviewedByUserId = userId;
        transaction.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? transaction.Notes
            : request.Notes.Trim();

        if (request.CreateRuleFromCorrection)
        {
            var rulePattern = ResolveRulePattern(request.RulePattern, transaction);
            var matchType = NormalizeMatchType(request.RuleMatchType, nameof(request.RuleMatchType));

            var rule = new CategorisationRule
            {
                TenantId = tenantId,
                UserId = userId,
                Pattern = rulePattern,
                Category = transaction.Category,
                Priority = request.RulePriority,
                IsActive = true,
                MatchType = matchType,
                CaseSensitive = false,
                MinAmount = null,
                MaxAmount = null,
                AppliesToAccountId = transaction.PersonalAccountId,
                CreatedFromUserCorrection = true,
                Scope = "User",
                ApprovalStatus = "Approved"
            };

            _financeDbContext.CategorisationRules.Add(rule);
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        return MapReviewItem(transaction);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task EnsureOwnedAccountAsync(
        Guid? personalAccountId,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!personalAccountId.HasValue)
        {
            return;
        }

        var accountExists = await _financeDbContext.PersonalAccounts
            .AnyAsync(
                account => account.Id == personalAccountId.Value
                    && account.TenantId == tenantId
                    && account.UserId == userId,
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException("Personal account not found.");
        }
    }

    private async Task<PersonalTransaction?> GetOwnedTransactionAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        return await _financeDbContext.PersonalTransactions
            .FirstOrDefaultAsync(
                item => item.Id == transactionId && item.TenantId == tenantId && item.UserId == userId,
                cancellationToken);
    }

    private async Task<List<CategorisationRule>> GetActiveRulesAsync(
        PersonalTransaction transaction,
        CancellationToken cancellationToken)
    {
        return await _financeDbContext.CategorisationRules
            .AsNoTracking()
            .Where(rule =>
                rule.TenantId == transaction.TenantId
                && rule.UserId == transaction.UserId
                && rule.IsActive
                && (rule.AppliesToAccountId == null || rule.AppliesToAccountId == transaction.PersonalAccountId))
            .OrderByDescending(rule => rule.Priority)
            .ToListAsync(cancellationToken);
    }

    private static void ApplyBestRule(PersonalTransaction transaction, IReadOnlyList<CategorisationRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!IsRuleMatch(rule, transaction))
            {
                continue;
            }

            transaction.Category = rule.Category;
            transaction.Confidence = 0.9m;
            transaction.CategorisedBy = "rule";
            transaction.ClassificationMethod = "rule_engine";
            return;
        }
    }

    private static bool IsRuleMatch(CategorisationRule rule, PersonalTransaction transaction)
    {
        if (rule.MinAmount.HasValue && transaction.Amount < rule.MinAmount.Value)
        {
            return false;
        }

        if (rule.MaxAmount.HasValue && transaction.Amount > rule.MaxAmount.Value)
        {
            return false;
        }

        var sourceText = ResolveSourceText(transaction);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return false;
        }

        var pattern = rule.Pattern ?? string.Empty;
        var matchType = string.IsNullOrWhiteSpace(rule.MatchType)
            ? "contains"
            : rule.MatchType.Trim().ToLowerInvariant();

        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return matchType switch
        {
            "contains" => sourceText.Contains(pattern, comparison),
            "exact" => string.Equals(sourceText, pattern, comparison),
            "startswith" => sourceText.StartsWith(pattern, comparison),
            "endswith" => sourceText.EndsWith(pattern, comparison),
            "regex" => Regex.IsMatch(sourceText, pattern, rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase),
            "amount_range" => true,
            _ => sourceText.Contains(pattern, comparison)
        };
    }

    private static string ResolveSourceText(PersonalTransaction transaction)
    {
        if (!string.IsNullOrWhiteSpace(transaction.Description))
        {
            return transaction.Description;
        }

        if (!string.IsNullOrWhiteSpace(transaction.Merchant))
        {
            return transaction.Merchant;
        }

        return transaction.Notes ?? string.Empty;
    }

    private static string ResolveRulePattern(string? requestedPattern, PersonalTransaction transaction)
    {
        if (!string.IsNullOrWhiteSpace(requestedPattern))
        {
            return requestedPattern.Trim();
        }

        var candidate = transaction.Merchant
            ?? transaction.Description
            ?? transaction.Notes;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new InvalidOperationException("Rule pattern is required when transaction text is unavailable.");
        }

        return candidate.Trim();
    }

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static string NormalizeMatchType(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        ValidateRequiredText(normalized, fieldName);
        return normalized!.ToLowerInvariant();
    }

    private static CategorisationRuleResponse MapRule(CategorisationRule rule)
    {
        return new CategorisationRuleResponse(
            rule.Id,
            rule.UserId,
            rule.Pattern,
            rule.Category,
            rule.Priority,
            rule.IsActive,
            rule.MatchType,
            rule.CaseSensitive,
            rule.MinAmount,
            rule.MaxAmount,
            rule.AppliesToAccountId,
            rule.CreatedFromUserCorrection,
            rule.Scope,
            rule.ApprovalStatus,
            rule.CreatedAt,
            rule.UpdatedAt);
    }

    private static ClassificationReviewItemResponse MapReviewItem(PersonalTransaction transaction)
    {
        return new ClassificationReviewItemResponse(
            transaction.Id,
            transaction.PersonalAccountId,
            transaction.OccurredAt,
            transaction.Amount,
            transaction.Currency,
            transaction.Merchant,
            transaction.Description,
            transaction.Category,
            transaction.Confidence,
            transaction.CategorisedBy,
            transaction.ClassificationMethod,
            transaction.ReviewStatus ?? "Pending",
            transaction.CreatedAt,
            transaction.UpdatedAt);
    }
}
