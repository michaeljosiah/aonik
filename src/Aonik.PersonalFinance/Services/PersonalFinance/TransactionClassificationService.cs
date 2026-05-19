using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class TransactionClassificationService : ITransactionClassificationService
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;
    private readonly ITransactionAiClassifier _aiClassifier;
    private readonly ILogger<TransactionClassificationService> _logger;

    public TransactionClassificationService(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator,
        ITransactionAiClassifier aiClassifier,
        ILogger<TransactionClassificationService> logger)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
        _aiClassifier = aiClassifier;
        _logger = logger;
    }

    public async Task<CategorisationRuleResponse> CreateRuleAsync(
        CreateCategorisationRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Pattern, nameof(request.Pattern));
        ValidateRequiredText(request.Category, nameof(request.Category));
        ValidateRequiredText(request.Scope, nameof(request.Scope));

        var matchType = NormalizeMatchType(request.MatchType, nameof(request.MatchType));
        ValidateRulePatternForMatchType(request.Pattern, matchType, nameof(request.Pattern));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        await EnsureOwnedAccountAsync(request.AppliesToAccountId, userId, tenantId, cancellationToken);

        var rule = new CategorisationRule
        {
            TenantId = tenantId,
            UserId = userId,
            Pattern = request.Pattern.Trim(),
            Category = request.Category.Trim(),
            SubCategory = string.IsNullOrWhiteSpace(request.SubCategory) ? null : request.SubCategory.Trim(),
            Priority = request.Priority,
            IsActive = true,
            MatchType = matchType,
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
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

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
        ValidateRequiredText(request.Scope, nameof(request.Scope));
        ValidateRequiredText(request.ApprovalStatus, nameof(request.ApprovalStatus));

        var matchType = NormalizeMatchType(request.MatchType, nameof(request.MatchType));
        ValidateRulePatternForMatchType(request.Pattern, matchType, nameof(request.Pattern));

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
        rule.SubCategory = string.IsNullOrWhiteSpace(request.SubCategory) ? null : request.SubCategory.Trim();
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.MatchType = matchType;
        rule.CaseSensitive = request.CaseSensitive;
        rule.MinAmount = request.MinAmount;
        rule.MaxAmount = request.MaxAmount;
        rule.AppliesToAccountId = request.AppliesToAccountId;
        rule.Scope = request.Scope.Trim();
        rule.ApprovalStatus = request.ApprovalStatus.Trim();

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

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
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
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

        // Step 1: Try deterministic rule matching
        var rules = await GetActiveRulesAsync(transaction, cancellationToken);
        ApplyBestRule(transaction, rules);

        // Step 2: If rules didn't match, try AI classification
        if (string.IsNullOrWhiteSpace(transaction.Category)
            || string.Equals(transaction.Category, TransactionCategoryReference.Uncategorized, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _aiClassifier.ClassifyAsync(transaction, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI classification fallback failed for transaction {TransactionId}", transactionId);
            }
        }

        transaction.ReviewStatus = "Reviewed";
        transaction.ReviewedByUserId = GetCurrentUserId();
        transaction.ReviewedAt = DateTime.UtcNow;

        // Step 3: If still unclassified, mark as Uncategorized
        if (string.IsNullOrWhiteSpace(transaction.Category))
        {
            transaction.Category = TransactionCategoryReference.Uncategorized;
            transaction.Confidence = 0;
            transaction.CategorisedBy = "manual";
            transaction.ClassificationMethod = "manual_fallback";
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
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
        transaction.TransactionType = TransactionCategoryReference.ResolveTransactionType(
            transaction.Category, transaction.Amount);
        transaction.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? transaction.Notes
            : request.Notes.Trim();

        if (request.CreateRuleFromCorrection)
        {
            var rulePattern = ResolveRulePattern(request.RulePattern, transaction);
            var matchType = NormalizeMatchType(request.RuleMatchType, nameof(request.RuleMatchType));
            ValidateRulePatternForMatchType(rulePattern, matchType, nameof(request.RulePattern));

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
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
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

    /// <summary>
    /// Loads active rules using scope-aware priority: User rules > Tenant rules > System rules.
    /// Within each scope, rules are ordered by descending priority.
    /// System rules use TenantId = Guid.Empty and UserId = Guid.Empty.
    /// Tenant rules use the transaction's TenantId and UserId = Guid.Empty.
    /// User rules use the transaction's TenantId and UserId.
    /// </summary>
    private async Task<List<CategorisationRule>> GetActiveRulesAsync(
        PersonalTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rules = await _financeDbContext.CategorisationRules
            .IncludeSoftDeleted()
            .AsNoTracking()
            .Where(rule =>
                rule.IsActive
                && !rule.IsDeleted
                && (rule.AppliesToAccountId == null || rule.AppliesToAccountId == transaction.PersonalAccountId)
                && (
                    // System rules (Scope = "System", global)
                    (rule.Scope == "System" && rule.TenantId == Guid.Empty && rule.UserId == Guid.Empty)
                    // Tenant rules (Scope = "Tenant", same tenant)
                    || (rule.Scope == "Tenant" && rule.TenantId == transaction.TenantId && rule.UserId == Guid.Empty)
                    // User rules (Scope = "User", same tenant + user)
                    || (rule.Scope == "User" && rule.TenantId == transaction.TenantId && rule.UserId == transaction.UserId)
                ))
            .ToListAsync(cancellationToken);

        // Sort: User scope first (highest priority), then Tenant, then System.
        // Within each scope, sort by descending Priority.
        return rules
            .OrderBy(rule => rule.Scope switch
            {
                "User" => 0,
                "Tenant" => 1,
                "System" => 2,
                _ => 3
            })
            .ThenByDescending(rule => rule.Priority)
            .ToList();
    }

    /// <summary>
    /// Applies the first matching rule with scope-aware confidence:
    /// User rule = 0.9, System/Tenant rule = 0.8.
    /// Also propagates SubCategory from the matched rule.
    /// </summary>
    private static void ApplyBestRule(PersonalTransaction transaction, IReadOnlyList<CategorisationRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!IsRuleMatch(rule, transaction))
            {
                continue;
            }

            transaction.Category = rule.Category;
            transaction.SubCategory = rule.SubCategory;

            // Confidence tier: User-created rule = 0.9, System/Tenant = 0.8
            transaction.Confidence = string.Equals(rule.Scope, "User", StringComparison.OrdinalIgnoreCase)
                ? 0.9m
                : 0.8m;

            transaction.CategorisedBy = "rule";
            transaction.ClassificationMethod = string.Equals(rule.Scope, "System", StringComparison.OrdinalIgnoreCase)
                ? "system_rule"
                : "rule_engine";
            transaction.TransactionType = TransactionCategoryReference.ResolveTransactionType(
                transaction.Category, transaction.Amount);
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

        var sourceTexts = ResolveSourceTexts(transaction);
        if (sourceTexts.Count == 0)
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
            "contains" => sourceTexts.Any(sourceText => sourceText.Contains(pattern, comparison)),
            "exact" => sourceTexts.Any(sourceText => string.Equals(sourceText, pattern, comparison)),
            "startswith" => sourceTexts.Any(sourceText => sourceText.StartsWith(pattern, comparison)),
            "endswith" => sourceTexts.Any(sourceText => sourceText.EndsWith(pattern, comparison)),
            "regex" => sourceTexts.Any(sourceText => IsRegexMatch(sourceText, pattern, rule.CaseSensitive)),
            "amount_range" => true,
            _ => sourceTexts.Any(sourceText => sourceText.Contains(pattern, comparison))
        };
    }

    private static bool IsRegexMatch(string sourceText, string pattern, bool caseSensitive)
    {
        try
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.IsMatch(sourceText, pattern, options, RegexMatchTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ResolveSourceTexts(PersonalTransaction transaction)
    {
        var sourceTexts = new List<string>();

        if (!string.IsNullOrWhiteSpace(transaction.Description))
        {
            sourceTexts.Add(transaction.Description.Trim());
        }

        if (!string.IsNullOrWhiteSpace(transaction.Merchant))
        {
            sourceTexts.Add(transaction.Merchant.Trim());
        }

        if (!string.IsNullOrWhiteSpace(transaction.Notes))
        {
            sourceTexts.Add(transaction.Notes.Trim());
        }

        return sourceTexts;
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

    private static void ValidateRulePatternForMatchType(string pattern, string matchType, string fieldName)
    {
        if (!string.Equals(matchType, "regex", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, RegexMatchTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("Pattern is not a valid regular expression.", fieldName, ex);
        }
    }

    private static CategorisationRuleResponse MapRule(CategorisationRule rule)
    {
        return new CategorisationRuleResponse(
            rule.Id,
            rule.UserId,
            rule.Pattern,
            rule.Category,
            rule.SubCategory,
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
            transaction.SubCategory,
            transaction.TransactionType,
            transaction.Confidence,
            transaction.CategorisedBy,
            transaction.ClassificationMethod,
            transaction.ReviewStatus ?? "Pending",
            transaction.CreatedAt,
            transaction.UpdatedAt);
    }
}
