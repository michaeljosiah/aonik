namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Provides a canonical set of transaction categories and mappings from provider-specific
/// category codes (e.g. Plaid <c>personal_finance_category.primary</c>) to standardized codes.
/// Also resolves <c>TransactionType</c> (Income, Expense, Transfer) from category and amount.
/// </summary>
/// <remarks>
/// The taxonomy is purpose-driven (what does this transaction mean to the user?) rather
/// than merchant-type-driven (what kind of business is this?). This follows the approach
/// outlined in Cleo's transaction enrichment architecture, where classification accuracy
/// improves when categories reflect user intent rather than counterparty type.
/// </remarks>
internal static class TransactionCategoryReference
{
    // ── Canonical category codes (26 categories) ───────────────────────────

    // Income
    public const string Income = "income";

    // Transfers
    public const string TransferIn = "transfer_in";
    public const string TransferOut = "transfer_out";
    public const string FamilySupport = "family_support";

    // Essentials
    public const string Housing = "housing";
    public const string Groceries = "groceries";
    public const string EatingOut = "eating_out";
    public const string Transport = "transport";
    public const string Bills = "bills";
    public const string Health = "health";
    public const string Education = "education";

    // Shopping
    public const string Shopping = "shopping";
    public const string PersonalCare = "personal_care";
    public const string Gifts = "gifts";

    // Lifestyle
    public const string Entertainment = "entertainment";
    public const string Subscriptions = "subscriptions";
    public const string Travel = "travel";
    public const string Fitness = "fitness";
    public const string Pets = "pets";

    // Financial
    public const string Savings = "savings";
    public const string Investments = "investments";
    public const string LoanPayments = "loan_payments";
    public const string BankFees = "bank_fees";

    // Services
    public const string Charity = "charity";

    // Other
    public const string Other = "other";
    public const string Uncategorized = "uncategorized";

    // ── Transaction type constants ─────────────────────────────────────────

    public const string TypeIncome = "Income";
    public const string TypeExpense = "Expense";
    public const string TypeTransfer = "Transfer";

    // ── Plaid primary → canonical code mapping ─────────────────────────────
    //
    // Plaid's personal_finance_category.primary values map to our canonical
    // codes. Since our taxonomy is finer-grained than Plaid's, some Plaid
    // categories map to a default and the Plaid detailed field can refine it
    // further via MapPlaidCategory().

    private static readonly Dictionary<string, string> PlaidPrimaryCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INCOME"] = Income,
        ["TRANSFER_IN"] = TransferIn,
        ["TRANSFER_OUT"] = TransferOut,
        ["FOOD_AND_DRINK"] = Groceries,              // Default; refined by detailed
        ["GENERAL_MERCHANDISE"] = Shopping,
        ["TRANSPORTATION"] = Transport,
        ["RENT_AND_UTILITIES"] = Bills,               // Default; refined by detailed
        ["TRAVEL"] = Travel,
        ["ENTERTAINMENT"] = Entertainment,
        ["PERSONAL_CARE"] = PersonalCare,
        ["GENERAL_SERVICES"] = Other,                 // Too vague; refined by detailed
        ["GOVERNMENT_AND_NON_PROFIT"] = Charity,      // Best default mapping
        ["HOME_IMPROVEMENT"] = Shopping,              // Maps to Shopping
        ["MEDICAL"] = Health,
        ["EDUCATION"] = Education,
        ["LOAN_PAYMENTS"] = LoanPayments,
        ["BANK_FEES"] = BankFees,
        ["OTHER"] = Other,
    };

    // ── Plaid detailed → canonical code refinement ─────────────────────────
    //
    // When the Plaid detailed category provides more specificity, we can
    // refine the primary mapping. Keys are "PRIMARY.DETAILED" (uppercased).
    // Values are (category, subcategory?) tuples.

    private static readonly Dictionary<string, (string Category, string? SubCategory)> PlaidDetailedCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // FOOD_AND_DRINK refinements
        ["FOOD_AND_DRINK.GROCERIES"] = (Groceries, "supermarket"),
        ["FOOD_AND_DRINK.RESTAURANT"] = (EatingOut, "restaurant"),
        ["FOOD_AND_DRINK.COFFEE"] = (EatingOut, "cafe"),
        ["FOOD_AND_DRINK.FAST_FOOD"] = (EatingOut, "fast_food"),
        ["FOOD_AND_DRINK.FOOD_AND_DRINK_OTHER"] = (Groceries, null),
        ["FOOD_AND_DRINK.BEER_WINE_AND_LIQUOR"] = (Groceries, "alcohol"),
        ["FOOD_AND_DRINK.VENDING_MACHINES"] = (EatingOut, null),

        // RENT_AND_UTILITIES refinements
        ["RENT_AND_UTILITIES.RENT"] = (Housing, "rent"),
        ["RENT_AND_UTILITIES.MORTGAGE"] = (Housing, "mortgage"),
        ["RENT_AND_UTILITIES.GAS_AND_ELECTRICITY"] = (Bills, "electricity"),
        ["RENT_AND_UTILITIES.WATER"] = (Bills, "water"),
        ["RENT_AND_UTILITIES.SEWAGE_AND_WASTE_MANAGEMENT"] = (Bills, "waste"),
        ["RENT_AND_UTILITIES.TELEPHONE"] = (Bills, "phone"),
        ["RENT_AND_UTILITIES.INTERNET"] = (Subscriptions, "software"),
        ["RENT_AND_UTILITIES.RENT_AND_UTILITIES_OTHER"] = (Bills, null),

        // ENTERTAINMENT refinements
        ["ENTERTAINMENT.MUSIC_AND_AUDIO"] = (Subscriptions, "music"),
        ["ENTERTAINMENT.TV_AND_MOVIES"] = (Subscriptions, "streaming"),
        ["ENTERTAINMENT.VIDEO_GAMES"] = (Entertainment, "gaming"),
        ["ENTERTAINMENT.CASINOS_AND_GAMBLING"] = (Entertainment, "gambling"),
        ["ENTERTAINMENT.SPORTING_EVENTS_AMUSEMENT_PARKS_AND_MUSEUMS"] = (Entertainment, "events"),
        ["ENTERTAINMENT.ENTERTAINMENT_OTHER"] = (Entertainment, null),

        // GENERAL_SERVICES refinements
        ["GENERAL_SERVICES.INSURANCE"] = (Bills, "insurance"),
        ["GENERAL_SERVICES.ACCOUNTING_AND_FINANCIAL_PLANNING"] = (Other, null),
        ["GENERAL_SERVICES.AUTOMOTIVE"] = (Transport, "car_maintenance"),
        ["GENERAL_SERVICES.CHILDCARE"] = (Other, null),
        ["GENERAL_SERVICES.CONSULTING_AND_LEGAL"] = (Other, null),
        ["GENERAL_SERVICES.EDUCATION"] = (Education, "courses"),
        ["GENERAL_SERVICES.POSTAGE_AND_SHIPPING"] = (Other, null),
        ["GENERAL_SERVICES.STORAGE"] = (Other, null),
        ["GENERAL_SERVICES.VETERINARY_SERVICES"] = (Pets, "vet"),
        ["GENERAL_SERVICES.GENERAL_SERVICES_OTHER"] = (Other, null),

        // TRANSFER_OUT refinements
        ["TRANSFER_OUT.SAVINGS"] = (Savings, null),
        ["TRANSFER_OUT.INVESTMENT"] = (Investments, null),
        ["TRANSFER_OUT.CHARITY"] = (Charity, "donation"),
        ["TRANSFER_OUT.TRANSFER_OUT_OTHER"] = (TransferOut, null),

        // GENERAL_MERCHANDISE refinements
        ["GENERAL_MERCHANDISE.CLOTHING_AND_ACCESSORIES"] = (Shopping, "clothing"),
        ["GENERAL_MERCHANDISE.ELECTRONICS"] = (Shopping, "electronics"),
        ["GENERAL_MERCHANDISE.DEPARTMENT_STORES"] = (Shopping, "department_store"),
        ["GENERAL_MERCHANDISE.DISCOUNT_STORES"] = (Shopping, null),
        ["GENERAL_MERCHANDISE.GIFTS_AND_NOVELTIES"] = (Gifts, "present"),
        ["GENERAL_MERCHANDISE.SPORTING_GOODS"] = (Fitness, "equipment"),
        ["GENERAL_MERCHANDISE.PET_SUPPLY_STORES"] = (Pets, "supplies"),
        ["GENERAL_MERCHANDISE.GENERAL_MERCHANDISE_OTHER"] = (Shopping, null),

        // PERSONAL_CARE refinements
        ["PERSONAL_CARE.GYMS_AND_FITNESS_CENTERS"] = (Fitness, "gym"),
        ["PERSONAL_CARE.PERSONAL_CARE_OTHER"] = (PersonalCare, null),

        // TRANSPORTATION refinements
        ["TRANSPORTATION.PARKING"] = (Transport, "parking"),
        ["TRANSPORTATION.PUBLIC_TRANSIT"] = (Transport, "public_transit"),
        ["TRANSPORTATION.TAXIS_AND_RIDE_SHARES"] = (Transport, "ride_hailing"),
        ["TRANSPORTATION.GAS"] = (Transport, "fuel"),
        ["TRANSPORTATION.TOLLS"] = (Transport, "tolls"),
        ["TRANSPORTATION.TRANSPORTATION_OTHER"] = (Transport, null),

        // GOVERNMENT_AND_NON_PROFIT refinements
        ["GOVERNMENT_AND_NON_PROFIT.DONATIONS"] = (Charity, "donation"),
        ["GOVERNMENT_AND_NON_PROFIT.TAX_PAYMENT"] = (Bills, "council_tax"),
        ["GOVERNMENT_AND_NON_PROFIT.GOVERNMENT_DEPARTMENTS_AND_AGENCIES"] = (Bills, null),
        ["GOVERNMENT_AND_NON_PROFIT.GOVERNMENT_AND_NON_PROFIT_OTHER"] = (Bills, null),
    };

    // ── Canonical code → display metadata ──────────────────────────────────

    private static readonly Dictionary<string, (string DisplayName, string GroupName, string IconName, int SortOrder)> CategoryMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        // Income
        [Income] = ("Income", "Income", "account_balance_wallet", 1),

        // Transfers
        [TransferIn] = ("Transfer In", "Transfers", "call_received", 2),
        [TransferOut] = ("Transfer Out", "Transfers", "call_made", 3),
        [FamilySupport] = ("Family Support", "Transfers", "family_restroom", 4),

        // Essentials
        [Housing] = ("Housing", "Essentials", "home", 10),
        [Groceries] = ("Groceries", "Essentials", "shopping_cart", 11),
        [EatingOut] = ("Eating Out", "Essentials", "restaurant", 12),
        [Transport] = ("Transport", "Essentials", "directions_car", 13),
        [Bills] = ("Bills", "Essentials", "receipt_long", 14),
        [Health] = ("Health", "Essentials", "favorite", 15),
        [Education] = ("Education", "Essentials", "school", 16),

        // Shopping
        [Shopping] = ("Shopping", "Shopping", "shopping_bag", 20),
        [PersonalCare] = ("Personal Care", "Shopping", "spa", 21),
        [Gifts] = ("Gifts", "Shopping", "card_giftcard", 22),

        // Lifestyle
        [Entertainment] = ("Entertainment", "Lifestyle", "movie", 30),
        [Subscriptions] = ("Subscriptions", "Lifestyle", "subscriptions", 31),
        [Travel] = ("Travel", "Lifestyle", "flight", 32),
        [Fitness] = ("Fitness", "Lifestyle", "fitness_center", 33),
        [Pets] = ("Pets", "Lifestyle", "pets", 34),

        // Financial
        [Savings] = ("Savings", "Financial", "savings", 40),
        [Investments] = ("Investments", "Financial", "trending_up", 41),
        [LoanPayments] = ("Loan Payments", "Financial", "money_off", 42),
        [BankFees] = ("Bank Fees", "Financial", "account_balance", 43),

        // Services
        [Charity] = ("Charity", "Services", "volunteer_activism", 50),

        // Other
        [Other] = ("Other", "Other", "more_horiz", 90),
        [Uncategorized] = ("Uncategorized", "Other", "help_outline", 99),
    };

    // ── Subcategory taxonomy (~90 subcategories) ───────────────────────────
    //
    // Subcategories provide finer-grained classification beneath the 26
    // top-level categories. The key is "parentCategory:subCategoryCode".
    // Subcategory codes use snake_case, same as parent categories.
    // Display names are user-facing; icons are optional (nullable).

    private static readonly Dictionary<string, SubCategoryMeta> SubCategoryMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Income ─────────────────────────────────────────────────────
        [$"{Income}:salary"] = new("Salary", "account_balance_wallet", 1),
        [$"{Income}:freelance"] = new("Freelance", "work", 2),
        [$"{Income}:benefits"] = new("Benefits", "health_and_safety", 3),
        [$"{Income}:refund"] = new("Refund", "replay", 4),
        [$"{Income}:interest"] = new("Interest", "percent", 5),
        [$"{Income}:rental_income"] = new("Rental Income", "home_work", 6),
        [$"{Income}:side_hustle"] = new("Side Hustle", "storefront", 7),

        // ── Transfer In ────────────────────────────────────────────────
        [$"{TransferIn}:own_account"] = new("Own Account", "swap_horiz", 1),
        [$"{TransferIn}:received_transfer"] = new("Received Transfer", "call_received", 2),

        // ── Transfer Out ───────────────────────────────────────────────
        [$"{TransferOut}:own_account"] = new("Own Account", "swap_horiz", 1),
        [$"{TransferOut}:sent_transfer"] = new("Sent Transfer", "call_made", 2),

        // ── Family Support ─────────────────────────────────────────────
        [$"{FamilySupport}:remittance"] = new("Remittance", "public", 1),
        [$"{FamilySupport}:family_allowance"] = new("Family Allowance", "family_restroom", 2),
        [$"{FamilySupport}:school_fees"] = new("School Fees", "school", 3),
        [$"{FamilySupport}:medical_support"] = new("Medical Support", "local_hospital", 4),

        // ── Housing ────────────────────────────────────────────────────
        [$"{Housing}:rent"] = new("Rent", "home", 1),
        [$"{Housing}:mortgage"] = new("Mortgage", "house", 2),
        [$"{Housing}:repairs"] = new("Repairs & Maintenance", "build", 3),
        [$"{Housing}:furnishing"] = new("Furnishing", "chair", 4),
        [$"{Housing}:property_tax"] = new("Property Tax", "receipt_long", 5),

        // ── Groceries ──────────────────────────────────────────────────
        [$"{Groceries}:supermarket"] = new("Supermarket", "store", 1),
        [$"{Groceries}:market"] = new("Market", "storefront", 2),
        [$"{Groceries}:online_grocery"] = new("Online Grocery", "local_shipping", 3),
        [$"{Groceries}:alcohol"] = new("Alcohol & Drinks", "local_bar", 4),

        // ── Eating Out ─────────────────────────────────────────────────
        [$"{EatingOut}:restaurant"] = new("Restaurant", "restaurant", 1),
        [$"{EatingOut}:fast_food"] = new("Fast Food", "fastfood", 2),
        [$"{EatingOut}:cafe"] = new("Café & Coffee", "local_cafe", 3),
        [$"{EatingOut}:delivery"] = new("Food Delivery", "delivery_dining", 4),
        [$"{EatingOut}:takeaway"] = new("Takeaway", "takeout_dining", 5),

        // ── Transport ──────────────────────────────────────────────────
        [$"{Transport}:fuel"] = new("Fuel", "local_gas_station", 1),
        [$"{Transport}:public_transit"] = new("Public Transit", "directions_bus", 2),
        [$"{Transport}:ride_hailing"] = new("Ride Hailing", "local_taxi", 3),
        [$"{Transport}:parking"] = new("Parking", "local_parking", 4),
        [$"{Transport}:car_maintenance"] = new("Car Maintenance", "car_repair", 5),
        [$"{Transport}:tolls"] = new("Tolls", "toll", 6),

        // ── Bills ──────────────────────────────────────────────────────
        [$"{Bills}:electricity"] = new("Electricity", "bolt", 1),
        [$"{Bills}:water"] = new("Water", "water_drop", 2),
        [$"{Bills}:gas"] = new("Gas", "gas_meter", 3),
        [$"{Bills}:phone"] = new("Phone & Mobile", "phone_android", 4),
        [$"{Bills}:internet"] = new("Internet", "wifi", 5),
        [$"{Bills}:insurance"] = new("Insurance", "shield", 6),
        [$"{Bills}:council_tax"] = new("Council Tax / Rates", "account_balance", 7),
        [$"{Bills}:waste"] = new("Waste & Sewage", "delete", 8),
        [$"{Bills}:tv_licence"] = new("TV Licence", "tv", 9),

        // ── Health ─────────────────────────────────────────────────────
        [$"{Health}:doctor"] = new("Doctor / GP", "medical_services", 1),
        [$"{Health}:pharmacy"] = new("Pharmacy", "local_pharmacy", 2),
        [$"{Health}:hospital"] = new("Hospital", "local_hospital", 3),
        [$"{Health}:dental"] = new("Dental", "dentistry", 4),
        [$"{Health}:optical"] = new("Optical", "visibility", 5),
        [$"{Health}:mental_health"] = new("Mental Health", "psychology", 6),

        // ── Education ──────────────────────────────────────────────────
        [$"{Education}:tuition"] = new("Tuition Fees", "school", 1),
        [$"{Education}:courses"] = new("Courses & Training", "menu_book", 2),
        [$"{Education}:books"] = new("Books & Materials", "auto_stories", 3),
        [$"{Education}:exams"] = new("Exams & Certification", "quiz", 4),

        // ── Shopping ───────────────────────────────────────────────────
        [$"{Shopping}:clothing"] = new("Clothing & Accessories", "checkroom", 1),
        [$"{Shopping}:electronics"] = new("Electronics", "devices", 2),
        [$"{Shopping}:home_goods"] = new("Home & Garden", "yard", 3),
        [$"{Shopping}:online"] = new("Online Shopping", "shopping_cart", 4),
        [$"{Shopping}:department_store"] = new("Department Store", "store", 5),

        // ── Personal Care ──────────────────────────────────────────────
        [$"{PersonalCare}:haircut"] = new("Haircut & Barber", "content_cut", 1),
        [$"{PersonalCare}:beauty"] = new("Beauty & Spa", "spa", 2),
        [$"{PersonalCare}:cosmetics"] = new("Cosmetics", "brush", 3),

        // ── Gifts ──────────────────────────────────────────────────────
        [$"{Gifts}:gift_card"] = new("Gift Card", "card_giftcard", 1),
        [$"{Gifts}:present"] = new("Present", "redeem", 2),
        [$"{Gifts}:flowers"] = new("Flowers", "local_florist", 3),

        // ── Entertainment ──────────────────────────────────────────────
        [$"{Entertainment}:cinema"] = new("Cinema", "movie", 1),
        [$"{Entertainment}:gaming"] = new("Gaming", "sports_esports", 2),
        [$"{Entertainment}:events"] = new("Events & Concerts", "confirmation_number", 3),
        [$"{Entertainment}:gambling"] = new("Gambling & Betting", "casino", 4),

        // ── Subscriptions ──────────────────────────────────────────────
        [$"{Subscriptions}:streaming"] = new("Streaming", "live_tv", 1),
        [$"{Subscriptions}:music"] = new("Music", "music_note", 2),
        [$"{Subscriptions}:software"] = new("Software", "computer", 3),
        [$"{Subscriptions}:news"] = new("News & Magazines", "newspaper", 4),
        [$"{Subscriptions}:cloud_storage"] = new("Cloud Storage", "cloud", 5),

        // ── Travel ─────────────────────────────────────────────────────
        [$"{Travel}:flights"] = new("Flights", "flight", 1),
        [$"{Travel}:hotel"] = new("Hotel & Accommodation", "hotel", 2),
        [$"{Travel}:car_rental"] = new("Car Rental", "car_rental", 3),
        [$"{Travel}:booking"] = new("Travel Booking", "luggage", 4),

        // ── Fitness ────────────────────────────────────────────────────
        [$"{Fitness}:gym"] = new("Gym Membership", "fitness_center", 1),
        [$"{Fitness}:sports"] = new("Sports & Activities", "sports_soccer", 2),
        [$"{Fitness}:equipment"] = new("Equipment", "sports_martial_arts", 3),

        // ── Pets ───────────────────────────────────────────────────────
        [$"{Pets}:food"] = new("Pet Food", "pets", 1),
        [$"{Pets}:vet"] = new("Vet", "veterinary", 2),
        [$"{Pets}:supplies"] = new("Pet Supplies", "shopping_bag", 3),

        // ── Savings ────────────────────────────────────────────────────
        [$"{Savings}:emergency_fund"] = new("Emergency Fund", "savings", 1),
        [$"{Savings}:goal_savings"] = new("Goal Savings", "flag", 2),
        [$"{Savings}:fixed_deposit"] = new("Fixed Deposit", "lock", 3),

        // ── Investments ────────────────────────────────────────────────
        [$"{Investments}:stocks"] = new("Stocks & Shares", "candlestick_chart", 1),
        [$"{Investments}:crypto"] = new("Crypto", "currency_bitcoin", 2),
        [$"{Investments}:funds"] = new("Funds & ISA", "pie_chart", 3),
        [$"{Investments}:pension"] = new("Pension", "elderly", 4),

        // ── Loan Payments ──────────────────────────────────────────────
        [$"{LoanPayments}:personal_loan"] = new("Personal Loan", "money_off", 1),
        [$"{LoanPayments}:bnpl"] = new("Buy Now Pay Later", "shopping_cart_checkout", 2),
        [$"{LoanPayments}:credit_card"] = new("Credit Card", "credit_card", 3),
        [$"{LoanPayments}:student_loan"] = new("Student Loan", "school", 4),

        // ── Bank Fees ──────────────────────────────────────────────────
        [$"{BankFees}:overdraft"] = new("Overdraft Fee", "warning", 1),
        [$"{BankFees}:atm"] = new("ATM Fee", "atm", 2),
        [$"{BankFees}:card_fee"] = new("Card Fee", "credit_card", 3),
        [$"{BankFees}:foreign_tx"] = new("Foreign Transaction Fee", "currency_exchange", 4),
        [$"{BankFees}:sms_alert"] = new("SMS Alert Fee", "sms", 5),

        // ── Charity ────────────────────────────────────────────────────
        [$"{Charity}:donation"] = new("Donation", "volunteer_activism", 1),
        [$"{Charity}:religious"] = new("Religious Giving", "mosque", 2),
        [$"{Charity}:crowdfunding"] = new("Crowdfunding", "group", 3),
    };

    private readonly record struct SubCategoryMeta(string DisplayName, string? IconName, int SortOrder);

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
        FamilySupport,
    };

    /// <summary>
    /// Maps Plaid category data to a canonical category code. Attempts to use the detailed
    /// category first for finer-grained classification, falling back to the primary mapping.
    /// Returns <c>null</c> if both inputs are null/empty, or <see cref="Other"/> if the
    /// Plaid primary category is not recognized.
    /// </summary>
    public static string? MapPlaidCategory(string? plaidPrimary, string? plaidDetailed = null)
    {
        var (category, _) = MapPlaidCategoryWithSubCategory(plaidPrimary, plaidDetailed);
        return category;
    }

    /// <summary>
    /// Maps Plaid category data to a canonical category code and optional subcategory.
    /// Attempts the detailed category first, falling back to the primary mapping.
    /// Returns <c>(null, null)</c> if both inputs are null/empty.
    /// </summary>
    public static (string? Category, string? SubCategory) MapPlaidCategoryWithSubCategory(
        string? plaidPrimary, string? plaidDetailed = null)
    {
        if (string.IsNullOrWhiteSpace(plaidPrimary))
        {
            return (null, null);
        }

        var primary = plaidPrimary.Trim();

        // Try detailed mapping first (e.g. "FOOD_AND_DRINK.RESTAURANT" → ("eating_out", "restaurant"))
        if (!string.IsNullOrWhiteSpace(plaidDetailed))
        {
            var compositeKey = $"{primary}.{plaidDetailed.Trim()}";
            if (PlaidDetailedCategoryMap.TryGetValue(compositeKey, out var detailed))
            {
                return (detailed.Category, detailed.SubCategory);
            }
        }

        // Fall back to primary mapping (no subcategory from primary-only mapping)
        return PlaidPrimaryCategoryMap.TryGetValue(primary, out var canonical)
            ? (canonical, null)
            : (Other, null);
    }

    /// <summary>
    /// Maps a Plaid <c>personal_finance_category.primary</c> value to a canonical code.
    /// Kept for backward compatibility; prefer <see cref="MapPlaidCategory"/> for new code.
    /// </summary>
    public static string? MapPlaidPrimaryCategory(string? plaidPrimary)
    {
        return MapPlaidCategory(plaidPrimary);
    }

    /// <summary>
    /// Resolves the <c>TransactionType</c> ("Income", "Expense", "Transfer") based on
    /// the canonical category code and the transaction amount.
    /// </summary>
    /// <remarks>
    /// Resolution priority:
    /// 1. If the category is a known transfer category -> "Transfer"
    /// 2. If the category is a known income category -> "Income"
    /// 3. If the amount is positive -> "Income"
    /// 4. Otherwise -> "Expense"
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
    /// Returns the icon name for a canonical category code, or <c>null</c> if unknown.
    /// </summary>
    public static string? GetIconName(string? categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            return null;
        }

        return CategoryMetadata.TryGetValue(categoryCode, out var meta) ? meta.IconName : null;
    }

    /// <summary>
    /// Returns <c>true</c> if the given code is a recognized canonical category.
    /// </summary>
    public static bool IsValidCategory(string? categoryCode)
    {
        return !string.IsNullOrWhiteSpace(categoryCode) && CategoryMetadata.ContainsKey(categoryCode);
    }

    /// <summary>
    /// Returns <c>true</c> if the given subcategory code is valid for the specified parent category.
    /// </summary>
    public static bool IsValidSubCategory(string? categoryCode, string? subCategoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode) || string.IsNullOrWhiteSpace(subCategoryCode))
        {
            return false;
        }

        return SubCategoryMetadata.ContainsKey($"{categoryCode}:{subCategoryCode}");
    }

    /// <summary>
    /// Returns the display name for a subcategory code under the given parent category.
    /// </summary>
    public static string? GetSubCategoryDisplayName(string? categoryCode, string? subCategoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode) || string.IsNullOrWhiteSpace(subCategoryCode))
        {
            return null;
        }

        return SubCategoryMetadata.TryGetValue($"{categoryCode}:{subCategoryCode}", out var meta)
            ? meta.DisplayName
            : null;
    }

    /// <summary>
    /// Returns all canonical categories with their metadata, suitable for seeding
    /// or populating the <c>TransactionCategories</c> reference table.
    /// </summary>
    public static IReadOnlyList<(string Code, string DisplayName, string GroupName, string IconName, int SortOrder)> GetAllCategories()
    {
        return CategoryMetadata
            .Select(kvp => (kvp.Key, kvp.Value.DisplayName, kvp.Value.GroupName, kvp.Value.IconName, kvp.Value.SortOrder))
            .OrderBy(item => item.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Returns all subcategories for a given parent category code, ordered by sort order.
    /// Returns an empty list if the category code is not recognized.
    /// </summary>
    public static IReadOnlyList<(string Code, string DisplayName, string? IconName, int SortOrder)> GetSubCategories(string categoryCode)
    {
        var prefix = $"{categoryCode}:";

        return SubCategoryMetadata
            .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kvp =>
            {
                var subCode = kvp.Key[prefix.Length..];
                return (subCode, kvp.Value.DisplayName, kvp.Value.IconName, kvp.Value.SortOrder);
            })
            .OrderBy(item => item.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Returns all subcategories across all parent categories, grouped by parent.
    /// Each item includes the parent category code, subcategory code, display name, icon, and sort order.
    /// </summary>
    public static IReadOnlyList<(string CategoryCode, string SubCategoryCode, string DisplayName, string? IconName, int SortOrder)> GetAllSubCategories()
    {
        return SubCategoryMetadata
            .Select(kvp =>
            {
                var separatorIndex = kvp.Key.IndexOf(':');
                var categoryCode = kvp.Key[..separatorIndex];
                var subCode = kvp.Key[(separatorIndex + 1)..];
                return (categoryCode, subCode, kvp.Value.DisplayName, kvp.Value.IconName, kvp.Value.SortOrder);
            })
            .OrderBy(item => item.categoryCode)
            .ThenBy(item => item.SortOrder)
            .ToList();
    }
}
