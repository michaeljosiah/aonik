namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Provides a canonical set of transaction categories and mappings from provider-specific
/// category codes (e.g. Plaid <c>personal_finance_category.primary</c>) to standardized codes.
/// Also resolves <c>TransactionType</c> (Income, Expense, Transfer) from category and amount.
/// </summary>
internal static class TransactionCategoryReference
{
    // ── Canonical category codes ───────────────────────────────────────────

    public const string Income = "income";
    public const string TransferIn = "transfer_in";
    public const string TransferOut = "transfer_out";
    public const string FoodAndDrink = "food_and_drink";
    public const string GeneralMerchandise = "general_merchandise";
    public const string Transportation = "transportation";
    public const string RentAndUtilities = "rent_and_utilities";
    public const string Travel = "travel";
    public const string Entertainment = "entertainment";
    public const string PersonalCare = "personal_care";
    public const string GeneralServices = "general_services";
    public const string GovernmentAndNonProfit = "government_and_non_profit";
    public const string HomeImprovement = "home_improvement";
    public const string Medical = "medical";
    public const string Education = "education";
    public const string LoanPayments = "loan_payments";
    public const string BankFees = "bank_fees";
    public const string Other = "other";
    public const string Uncategorized = "uncategorized";

    // ── Transaction type constants ─────────────────────────────────────────

    public const string TypeIncome = "Income";
    public const string TypeExpense = "Expense";
    public const string TypeTransfer = "Transfer";

    // ── Plaid primary → canonical code mapping ─────────────────────────────

    private static readonly Dictionary<string, string> PlaidPrimaryCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INCOME"] = Income,
        ["TRANSFER_IN"] = TransferIn,
        ["TRANSFER_OUT"] = TransferOut,
        ["FOOD_AND_DRINK"] = FoodAndDrink,
        ["GENERAL_MERCHANDISE"] = GeneralMerchandise,
        ["TRANSPORTATION"] = Transportation,
        ["RENT_AND_UTILITIES"] = RentAndUtilities,
        ["TRAVEL"] = Travel,
        ["ENTERTAINMENT"] = Entertainment,
        ["PERSONAL_CARE"] = PersonalCare,
        ["GENERAL_SERVICES"] = GeneralServices,
        ["GOVERNMENT_AND_NON_PROFIT"] = GovernmentAndNonProfit,
        ["HOME_IMPROVEMENT"] = HomeImprovement,
        ["MEDICAL"] = Medical,
        ["EDUCATION"] = Education,
        ["LOAN_PAYMENTS"] = LoanPayments,
        ["BANK_FEES"] = BankFees,
        ["OTHER"] = Other,
    };

    // ── Canonical code → display name ──────────────────────────────────────

    private static readonly Dictionary<string, (string DisplayName, string GroupName, int SortOrder)> CategoryMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        [Income] = ("Income", "Income", 1),
        [TransferIn] = ("Transfer In", "Transfers", 2),
        [TransferOut] = ("Transfer Out", "Transfers", 3),
        [FoodAndDrink] = ("Food & Drink", "Essentials", 10),
        [RentAndUtilities] = ("Rent & Utilities", "Essentials", 11),
        [Transportation] = ("Transportation", "Essentials", 12),
        [Medical] = ("Medical", "Essentials", 13),
        [Education] = ("Education", "Essentials", 14),
        [GeneralMerchandise] = ("General Merchandise", "Shopping", 20),
        [HomeImprovement] = ("Home Improvement", "Shopping", 21),
        [PersonalCare] = ("Personal Care", "Shopping", 22),
        [Entertainment] = ("Entertainment", "Lifestyle", 30),
        [Travel] = ("Travel", "Lifestyle", 31),
        [GeneralServices] = ("General Services", "Services", 40),
        [GovernmentAndNonProfit] = ("Government & Non-Profit", "Services", 41),
        [LoanPayments] = ("Loan Payments", "Financial", 50),
        [BankFees] = ("Bank Fees", "Financial", 51),
        [Other] = ("Other", "Other", 90),
        [Uncategorized] = ("Uncategorized", "Other", 99),
    };

    // ── Income-type categories ─────────────────────────────────────────────

    private static readonly HashSet<string> IncomeCategoryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        Income,
        TransferIn,
    };

    private static readonly HashSet<string> TransferCategoryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        TransferIn,
        TransferOut,
    };

    /// <summary>
    /// Maps a Plaid <c>personal_finance_category.primary</c> value (e.g. "FOOD_AND_DRINK")
    /// to the canonical category code (e.g. "food_and_drink").
    /// Returns <c>null</c> if the input is null/empty, or the <see cref="Other"/> code
    /// if the Plaid category is not recognized.
    /// </summary>
    public static string? MapPlaidPrimaryCategory(string? plaidPrimary)
    {
        if (string.IsNullOrWhiteSpace(plaidPrimary))
        {
            return null;
        }

        return PlaidPrimaryCategoryMap.TryGetValue(plaidPrimary.Trim(), out var canonical)
            ? canonical
            : Other;
    }

    /// <summary>
    /// Resolves the <c>TransactionType</c> ("Income", "Expense", "Transfer") based on
    /// the canonical category code and the transaction amount.
    /// </summary>
    /// <remarks>
    /// Resolution priority:
    /// 1. If the category is a known transfer category → "Transfer"
    /// 2. If the category is a known income category → "Income"
    /// 3. If the amount is positive → "Income"
    /// 4. Otherwise → "Expense"
    /// </remarks>
    public static string ResolveTransactionType(string? categoryCode, decimal amount)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            if (TransferCategoryCodes.Contains(categoryCode))
            {
                return TypeTransfer;
            }

            if (IncomeCategoryCodes.Contains(categoryCode))
            {
                return TypeIncome;
            }
        }

        return amount > 0 ? TypeIncome : TypeExpense;
    }

    /// <summary>
    /// Returns the display name for a canonical category code, or <c>null</c> if unknown.
    /// </summary>
    public static string? GetDisplayName(string? categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            return null;
        }

        return CategoryMetadata.TryGetValue(categoryCode, out var meta) ? meta.DisplayName : null;
    }

    /// <summary>
    /// Returns all canonical categories with their metadata, suitable for seeding
    /// or populating the <c>TransactionCategories</c> reference table.
    /// </summary>
    public static IReadOnlyList<(string Code, string DisplayName, string GroupName, int SortOrder)> GetAllCategories()
    {
        return CategoryMetadata
            .Select(kvp => (kvp.Key, kvp.Value.DisplayName, kvp.Value.GroupName, kvp.Value.SortOrder))
            .OrderBy(item => item.SortOrder)
            .ToList();
    }
}
