using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Provides pre-seeded system-level categorisation rules for common merchants
/// in the UK and Africa (Ghana, Nigeria, Kenya). These rules run with
/// <c>Scope = "System"</c>, <c>TenantId = Guid.Empty</c>, <c>UserId = Guid.Empty</c>,
/// giving them the lowest scope priority (user rules > tenant rules > system rules).
/// Confidence when matched: 0.8.
/// </summary>
internal static class SystemCategorisationRuleSeed
{
    /// <summary>
    /// Returns all system-level categorisation rules, ready for bulk insert.
    /// Each rule uses <c>MatchType = "contains"</c> (case-insensitive) unless noted.
    /// Priority values within the seed are relative; higher-priority = more specific merchant.
    /// </summary>
    public static IReadOnlyList<CategorisationRule> GetSystemRules()
    {
        var rules = new List<CategorisationRule>();

        // ── UK Groceries ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Groceries, "supermarket", 100,
            "Tesco", "Sainsbury", "Asda", "Morrisons", "Aldi", "Lidl", "Waitrose",
            "Co-op", "Co-operative", "M&S Food", "Iceland", "Ocado", "Farmfoods"));

        // ── UK Eating Out ─────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "restaurant", 100,
            "Nando", "Wagamama", "Five Guys"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "fast_food", 100,
            "McDonald", "McDonalds", "KFC", "Burger King", "Dominos",
            "Pizza Hut", "Greggs"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "cafe", 100,
            "Pret A Manger", "Pret a Manger", "Costa Coffee", "Starbucks", "Caffe Nero"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "delivery", 100,
            "Deliveroo", "Just Eat", "Uber Eats"));

        // ── UK Transport ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "public_transit", 100,
            "TFL", "Transport for London", "Trainline", "National Rail"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "ride_hailing", 100,
            "Uber", "Bolt", "Addison Lee"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "fuel", 100,
            "BP", "Shell", "Esso", "Texaco"));

        // ── UK Bills / Utilities ──────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "electricity", 100,
            "British Gas", "EDF Energy", "SSE", "Scottish Power", "E.ON",
            "Octopus Energy", "Bulb"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "water", 100,
            "Thames Water", "United Utilities"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "phone", 100,
            "EE", "Three", "Vodafone UK", "O2", "Giffgaff"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "internet", 100,
            "Sky", "Virgin Media", "BT"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "tv_licence", 100,
            "TV Licence"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "council_tax", 100,
            "Council Tax"));

        // ── UK Housing ────────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Housing, "rent", 100,
            "Rightmove", "OpenRent", "Foxtons"));

        // ── UK Shopping ───────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "online", 100,
            "Amazon", "Amazon.co.uk", "ASOS"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "department_store", 100,
            "Argos", "John Lewis", "TK Maxx"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "clothing", 100,
            "Primark", "H&M", "Zara", "Next"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "electronics", 100,
            "Currys"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, null, 100,
            "Boots", "Superdrug", "Halfords"));

        // ── UK Subscriptions ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "streaming", 100,
            "Netflix", "Disney Plus", "Disney+", "Amazon Prime", "Now TV"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "music", 100,
            "Spotify", "Audible", "YouTube Premium", "Crunchyroll"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "software", 100,
            "Apple.com/bill", "Adobe", "Microsoft 365", "ChatGPT", "OpenAI"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "cloud_storage", 100,
            "iCloud", "Google Storage"));

        // ── UK Entertainment ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Entertainment, "cinema", 100,
            "Cineworld", "Odeon", "Vue Cinema"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Entertainment, "events", 100,
            "Ticketmaster", "Eventbrite"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Entertainment, "gaming", 100,
            "Steam", "PlayStation", "Xbox", "Nintendo"));

        // ── UK Health ─────────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "doctor", 100,
            "NHS", "Bupa"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "optical", 100,
            "Specsavers"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "pharmacy", 100,
            "LloydsPharmacy", "Lloyds Pharmacy"));

        // ── UK Fitness ────────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Fitness, "gym", 100,
            "PureGym", "The Gym Group", "David Lloyd", "Nuffield Health",
            "Virgin Active", "JD Gyms"));

        // ── UK Charity ────────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Charity, "donation", 100,
            "JustGiving", "GoFundMe", "British Red Cross", "Oxfam",
            "Cancer Research"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Charity, "crowdfunding", 100,
            "UNICEF"));

        // ── UK Travel ─────────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Travel, "booking", 100,
            "Booking.com", "Skyscanner", "Hotels.com", "Expedia"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Travel, "hotel", 100,
            "Airbnb"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Travel, "flights", 100,
            "Ryanair", "EasyJet", "British Airways"));

        // ── UK Bank Fees ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, "overdraft", 100,
            "Overdraft Fee"));
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, "card_fee", 100,
            "Monthly Account Fee", "Card Replacement Fee"));
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, "foreign_tx", 100,
            "Foreign Transaction Fee"));
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, "atm", 100,
            "ATM Fee"));

        // ── UK Pets ───────────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Pets, "supplies", 100,
            "Pets at Home"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Pets, "vet", 100,
            "Vets4Pets"));

        // ── UK Personal Care ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.PersonalCare, "beauty", 100,
            "Treatwell"));
        rules.AddRange(CreateRules(TransactionCategoryReference.PersonalCare, "haircut", 100,
            "Headmasters"));

        // ── UK Education ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Education, "tuition", 100,
            "Student Loans Company", "SLC"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Education, "courses", 100,
            "Udemy", "Coursera", "Skillshare"));

        // ── UK Investments ────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Investments, "stocks", 100,
            "Trading 212", "Freetrade", "Hargreaves Lansdown", "Vanguard",
            "Nutmeg", "AJ Bell"));

        // ── UK Loan Payments ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.LoanPayments, "bnpl", 100,
            "Klarna", "Clearpay", "Afterpay"));
        rules.AddRange(CreateRules(TransactionCategoryReference.LoanPayments, "credit_card", 100,
            "PayPal Credit"));

        // ══════════════════════════════════════════════════════════════════
        // GHANA
        // ══════════════════════════════════════════════════════════════════

        // ── Ghana Groceries ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Groceries, "supermarket", 100,
            "Shoprite", "Melcom", "Palace", "Game", "Koala",
            "Marina Mall", "Max Mart", "Accra Mall"));

        // ── Ghana Eating Out ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "fast_food", 100,
            "Papaye", "KFC Ghana", "Chicken Republic", "Burger King Ghana"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "restaurant", 100,
            "Marwako", "Buka"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "delivery", 100,
            "Glovo"));

        // ── Ghana Bills / Telecoms ────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "phone", 100,
            "MTN Ghana", "MTN MoMo", "Vodafone Ghana", "AirtelTigo",
            "Telecel Ghana"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "electricity", 100,
            "ECG"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "water", 100,
            "Ghana Water"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, null, 100,
            "DSTV Ghana", "GOtv Ghana"));

        // ── Ghana Transport ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "ride_hailing", 100,
            "Bolt Ghana", "Uber Ghana", "Yango Ghana"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "fuel", 100,
            "TotalEnergies Ghana", "Shell Ghana", "Goil"));

        // ── Ghana Shopping ────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "online", 100,
            "Jumia Ghana", "Tonaton", "Jiji Ghana"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "electronics", 100,
            "Franko Trading"));

        // ── Ghana Family Support (Remittances) ───────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.FamilySupport, "remittance", 100,
            "WorldRemit", "Remitly", "Sendwave", "Western Union",
            "MoneyGram", "Chipper Cash"));

        // ── Ghana Health ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, null, 100,
            "NHIA"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "hospital", 100,
            "Korle Bu", "Lister Hospital"));

        // ── Ghana Education ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Education, "tuition", 100,
            "University of Ghana", "KNUST", "UCC", "Ashesi"));

        // ══════════════════════════════════════════════════════════════════
        // NIGERIA
        // ══════════════════════════════════════════════════════════════════

        // ── Nigeria Groceries ─────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Groceries, "supermarket", 100,
            "Shoprite Nigeria", "SPAR Nigeria", "Hubmart", "Justrite",
            "Ebeano", "Prince Ebeano", "Next Cash and Carry"));

        // ── Nigeria Eating Out ────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "fast_food", 100,
            "Chicken Republic", "KFC Nigeria", "Dominos Nigeria"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "restaurant", 100,
            "The Place", "Mr Biggs", "Tantalizers", "Kilimanjaro"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "delivery", 100,
            "Chowdeck", "Glovo Nigeria"));

        // ── Nigeria Bills / Telecoms ──────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "phone", 100,
            "MTN Nigeria", "Airtel Nigeria", "Glo", "9mobile"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, null, 100,
            "DSTV", "GOtv", "Startimes"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "electricity", 100,
            "IBEDC", "EKEDC", "AEDC", "PHED", "IKEDC", "BEDC", "NEPA"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "water", 100,
            "Lagos Water"));

        // ── Nigeria Transport ─────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "ride_hailing", 100,
            "Bolt Nigeria", "Uber Nigeria", "InDrive"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "fuel", 100,
            "TotalEnergies Nigeria", "Oando", "Mobil", "NNPC"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, null, 100,
            "Dana Air", "Air Peace", "Arik Air"));

        // ── Nigeria Shopping ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "online", 100,
            "Jumia", "Konga", "Jiji", "PayPorte"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "electronics", 100,
            "Slot", "Computer Village"));

        // ── Nigeria Family Support ────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.FamilySupport, null, 100,
            "Flutterwave", "OPay Transfer", "PalmPay Transfer",
            "Kuda Transfer"));

        // ── Nigeria Bank Fees ─────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, null, 100,
            "Stamp Duty", "VAT", "NIBSS Fee"));
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, "sms_alert", 100,
            "SMS Alert Fee"));
        rules.AddRange(CreateRules(TransactionCategoryReference.BankFees, "card_fee", 100,
            "Card Maintenance Fee", "Transfer Charge"));

        // ── Nigeria Health ────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "hospital", 100,
            "Reddington Hospital", "EHA Clinics"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "pharmacy", 100,
            "Medplus Pharmacy", "HealthPlus Pharmacy"));

        // ── Nigeria Education ─────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Education, "tuition", 100,
            "UNILAG", "OAU", "LASU", "Covenant University"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Education, "exams", 100,
            "WAEC", "JAMB", "NECO"));

        // ── Nigeria Subscriptions ─────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "streaming", 100,
            "Showmax", "IrokoTV"));

        // ══════════════════════════════════════════════════════════════════
        // KENYA
        // ══════════════════════════════════════════════════════════════════

        // ── Kenya Groceries ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Groceries, "supermarket", 100,
            "Naivas", "Quickmart", "Carrefour Kenya", "Chandarana",
            "Cleanshelf"));

        // ── Kenya Eating Out ──────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "cafe", 100,
            "Java House", "Artcaffe"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "fast_food", 100,
            "KFC Kenya", "Chicken Inn", "Pizza Inn Kenya"));
        rules.AddRange(CreateRules(TransactionCategoryReference.EatingOut, "delivery", 100,
            "Uber Eats Kenya", "Glovo Kenya"));

        // ── Kenya Bills / Telecoms ────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "phone", 100,
            "Safaricom", "M-Pesa", "MPESA", "Airtel Kenya"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "electricity", 100,
            "Kenya Power", "KPLC"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, "water", 100,
            "Nairobi Water"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Bills, null, 100,
            "DSTV Kenya", "GOtv Kenya", "Zuku"));

        // ── Kenya Transport ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "ride_hailing", 100,
            "Bolt Kenya", "Uber Kenya", "Little Cab"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, "fuel", 100,
            "TotalEnergies Kenya", "Shell Kenya"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Transport, null, 100,
            "Kenya Airways", "Jambojet"));

        // ── Kenya Shopping ────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "online", 100,
            "Jumia Kenya", "Kilimall", "Jiji Kenya", "Masoko"));

        // ── Kenya Family Support ──────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.FamilySupport, null, 100,
            "M-Pesa Send", "Equity Transfer"));

        // ── Kenya Health ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, "hospital", 100,
            "Nairobi Hospital", "Aga Khan Hospital"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Health, null, 100,
            "NHIF"));

        // ── Kenya Education ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Education, "tuition", 100,
            "University of Nairobi", "Kenyatta University", "JKUAT",
            "Strathmore"));

        // ══════════════════════════════════════════════════════════════════
        // CROSS-MARKET (Global / Multi-region)
        // ══════════════════════════════════════════════════════════════════

        // ── Global Subscriptions ──────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "software", 90,
            "GOOGLE *", "Apple.com", "APPLE.COM"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "streaming", 90,
            "AMZN", "HBO Max", "Paramount+", "Hulu", "Twitch"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Subscriptions, "music", 90,
            "Deezer"));

        // ── Global Shopping ───────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Shopping, "online", 90,
            "AliExpress", "Alibaba", "eBay", "Wish.com", "Shein", "Temu"));

        // ── Global Money Transfer / Family Support ────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.FamilySupport, "remittance", 90,
            "Wise Transfer", "TransferWise", "Remitly", "Azimo"));

        // ── Global Savings ────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Savings, null, 100,
            "PiggyVest", "Cowrywise", "Risevest", "Bamboo"));

        // ── Global Investments ────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Investments, "crypto", 100,
            "Binance", "Luno", "Quidax", "Roqqu", "Coinbase"));

        // ── Global Charity ────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Charity, "donation", 90,
            "Charity", "Donation"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Charity, "religious", 90,
            "Zakat", "Tithe", "Church Offering"));

        // ── Global Gifts ──────────────────────────────────────────────────
        rules.AddRange(CreateRules(TransactionCategoryReference.Gifts, "gift_card", 90,
            "Gift Card"));
        rules.AddRange(CreateRules(TransactionCategoryReference.Gifts, "present", 90,
            "Hallmark"));

        // Deduplicate by Id (deterministic from category+pattern) to prevent
        // duplicate key errors in EF Core HasData.
        return rules
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Fixed seed date for all system rules (consistent with TransactionCategoryConfiguration).
    /// </summary>
    private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Creates a batch of system-scope categorisation rules for the given patterns.
    /// All rules use <c>contains</c> match type, case-insensitive.
    /// Includes all AuditableEntity fields required by EF Core HasData.
    /// </summary>
    private static IEnumerable<CategorisationRule> CreateRules(
        string category,
        string? subCategory,
        int priority,
        params string[] patterns)
    {
        return patterns.Select(pattern => new CategorisationRule
        {
            Id = CreateDeterministicId(category, pattern),
            TenantId = Guid.Empty,
            UserId = Guid.Empty,
            Pattern = pattern,
            Category = category,
            SubCategory = subCategory,
            Priority = priority,
            IsActive = true,
            MatchType = "contains",
            CaseSensitive = false,
            MinAmount = null,
            MaxAmount = null,
            AppliesToAccountId = null,
            CreatedFromUserCorrection = false,
            Scope = "System",
            ApprovalStatus = "Approved",
            CreatedAt = SeedDate,
            IsDeleted = false,
            RowVersion = [],
        });
    }

    /// <summary>
    /// Creates a deterministic GUID from category + pattern so rules are idempotent
    /// and can be re-seeded without creating duplicates.
    /// </summary>
    private static Guid CreateDeterministicId(string category, string pattern)
    {
        var input = $"system-rule:{category}:{pattern.ToLowerInvariant()}";
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }
}
