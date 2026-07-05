using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Centralised normalisation helpers for customer-insight snapshot generation:
/// currency casing, key/display normalisation, status checks and the static
/// category-set lookups used to classify expenses.
/// </summary>
internal static class CustomerInsightNormalization
{
    public static readonly HashSet<string> FixedExpenseCategories =
    [
        TransactionCategoryReference.Housing,
        TransactionCategoryReference.Bills,
        TransactionCategoryReference.Subscriptions,
        TransactionCategoryReference.LoanPayments,
        TransactionCategoryReference.BankFees
    ];

    public static readonly HashSet<string> EssentialExpenseCategories =
    [
        TransactionCategoryReference.Housing,
        TransactionCategoryReference.Groceries,
        TransactionCategoryReference.Bills,
        TransactionCategoryReference.Health,
        TransactionCategoryReference.Education,
        TransactionCategoryReference.Transport,
        TransactionCategoryReference.LoanPayments,
        TransactionCategoryReference.FamilySupport
    ];

    public static readonly HashSet<string> SavingsContributionCategories =
    [
        TransactionCategoryReference.Savings,
        TransactionCategoryReference.Investments
    ];

    public static readonly HashSet<string> TransferCategories =
    [
        TransactionCategoryReference.TransferIn,
        TransactionCategoryReference.TransferOut
    ];

    public static bool IsActiveStatus(string? status) =>
        string.Equals(status?.Trim(), "active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status?.Trim(), "current", StringComparison.OrdinalIgnoreCase);

    public static bool IsArchivedStatus(string? status) =>
        string.Equals(status?.Trim(), "archived", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "UNK" : value.Trim().ToUpperInvariant();

    public static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static string NormalizeLower(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? (fallback ?? string.Empty)
            : value.Trim().ToLowerInvariant();

    public static string? NormalizeDisplay(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
