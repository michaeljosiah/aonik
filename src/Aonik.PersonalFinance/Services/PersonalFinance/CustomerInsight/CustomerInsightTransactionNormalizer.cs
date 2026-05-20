using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Converts raw <see cref="PersonalTransaction"/> rows into the denormalised
/// <see cref="NormalizedTransaction"/> shape every other insight builder consumes,
/// including the kind classification (transfer/income/expense) used downstream.
/// </summary>
internal static class CustomerInsightTransactionNormalizer
{
    public static NormalizedTransaction Normalize(
        PersonalTransaction transaction,
        IReadOnlyDictionary<Guid, string> accountNameById)
    {
        var currency = CustomerInsightNormalization.NormalizeCurrency(transaction.Currency);
        var category = CustomerInsightNormalization.NormalizeLower(transaction.Category, TransactionCategoryReference.Uncategorized);
        var subCategory = CustomerInsightNormalization.NormalizeLower(transaction.SubCategory, null);
        var merchantDisplay = string.IsNullOrWhiteSpace(transaction.Merchant) ? "Unknown Merchant" : transaction.Merchant.Trim();
        var merchantKey = CustomerInsightNormalization.NormalizeKey(transaction.Merchant);
        var description = CustomerInsightNormalization.NormalizeDisplay(transaction.Description) ?? string.Empty;
        var sourceDisplay = !string.IsNullOrWhiteSpace(merchantKey)
            ? merchantDisplay
            : !string.IsNullOrWhiteSpace(description)
                ? description
                : category;
        var sourceKey = CustomerInsightNormalization.NormalizeKey(sourceDisplay);
        var normalizedKind = DeriveNormalizedKind(transaction, category, subCategory);

        var accountName = transaction.PersonalAccountId.HasValue
            && accountNameById.TryGetValue(transaction.PersonalAccountId.Value, out var resolvedName)
                ? resolvedName
                : "Unassigned";

        return new NormalizedTransaction(
            transaction.Id,
            transaction.PersonalAccountId,
            accountName,
            transaction.OccurredAt,
            transaction.Amount,
            currency,
            merchantDisplay,
            merchantKey,
            category,
            subCategory,
            normalizedKind,
            sourceDisplay,
            sourceKey,
            normalizedKind == TransactionCategoryReference.TypeTransfer,
            normalizedKind == TransactionCategoryReference.TypeIncome,
            normalizedKind == TransactionCategoryReference.TypeExpense);
    }

    public static List<NormalizedTransaction> FilterByWindow(
        IEnumerable<NormalizedTransaction> transactions,
        DateTime startUtc,
        DateTime endUtc)
    {
        return transactions
            .Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc <= endUtc)
            .ToList();
    }

    private static string DeriveNormalizedKind(PersonalTransaction transaction, string category, string? subCategory)
    {
        var transactionType = CustomerInsightNormalization.NormalizeKey(transaction.TransactionType);
        if (transactionType == CustomerInsightNormalization.NormalizeKey(TransactionCategoryReference.TypeTransfer))
        {
            return TransactionCategoryReference.TypeTransfer;
        }

        if (CustomerInsightNormalization.TransferCategories.Contains(category)
            || string.Equals(subCategory, "own_account", StringComparison.OrdinalIgnoreCase))
        {
            return TransactionCategoryReference.TypeTransfer;
        }

        if (transactionType == CustomerInsightNormalization.NormalizeKey(TransactionCategoryReference.TypeIncome) || transaction.Amount > 0m)
        {
            return TransactionCategoryReference.TypeIncome;
        }

        if (transactionType == CustomerInsightNormalization.NormalizeKey(TransactionCategoryReference.TypeExpense) || transaction.Amount < 0m)
        {
            return TransactionCategoryReference.TypeExpense;
        }

        return "Other";
    }
}
