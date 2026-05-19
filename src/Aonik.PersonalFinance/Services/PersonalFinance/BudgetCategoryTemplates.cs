namespace Aonik.Finance.Services.PersonalFinance;

internal sealed record BudgetCategoryTemplate(
    string Id,
    string Name,
    string? Description,
    int IconCodePoint,
    string AccentRole,
    string? LinkedSpendingCategoryId);

internal static class BudgetCategoryTemplates
{
    private static readonly Dictionary<string, BudgetCategoryTemplate> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["housing"] = new("housing", "Housing", "Track rent, repairs, and household supplies.", 0xf085, "primary", TransactionCategoryReference.Housing),
        ["groceries"] = new("groceries", "Food & Groceries", "Supermarket runs, fresh market, and coffee stops.", 0xe56b, "success", TransactionCategoryReference.Groceries),
        ["transport"] = new("transport", "Transport", "Fuel, ride apps, and public transit fares.", 0xe1d1, "warning", TransactionCategoryReference.Transport),
        ["utilities"] = new("utilities", "Utilities", "Electricity, water, and internet bills.", 0xe90f, "info", TransactionCategoryReference.Bills),
        ["personal"] = new("personal", "Personal Care", "Grooming, pharmacy, and gym memberships.", 0xeb4c, "accent", TransactionCategoryReference.PersonalCare),
        ["eating-out"] = new("eating-out", "Eating Out", "Restaurants, takeaways, and dining with friends.", 0xe56c, "warning", TransactionCategoryReference.EatingOut),
        ["shopping"] = new("shopping", "Shopping", "Clothing, electronics, and everyday purchases.", 0xf37a, "accent", TransactionCategoryReference.Shopping),
        ["entertainment"] = new("entertainment", "Entertainment", "Movies, streaming, games, and nights out.", 0xe02c, "primary", TransactionCategoryReference.Entertainment),
        ["bills"] = new("bills", "Bills", "Phone, insurance, and recurring monthly bills.", 0xef6b, "danger", TransactionCategoryReference.Bills),
        ["health"] = new("health", "Health", "Doctor visits, prescriptions, and wellness.", 0xe87e, "danger", TransactionCategoryReference.Health),
        ["education"] = new("education", "Education", "Tuition, books, courses, and learning materials.", 0xe80c, "info", TransactionCategoryReference.Education),
        ["gifts"] = new("gifts", "Gifts", "Birthday, holiday, and special occasion presents.", 0xe8f6, "accent", TransactionCategoryReference.Gifts),
        ["travel"] = new("travel", "Travel", "Flights, hotels, and holiday spending.", 0xe539, "primary", TransactionCategoryReference.Travel),
        ["savings"] = new("savings", "Savings", "Emergency fund, rainy day, and saving goals.", 0xf14f, "success", TransactionCategoryReference.Savings),
        ["subscriptions"] = new("subscriptions", "Subscriptions", "Streaming, apps, memberships, and recurring charges.", 0xe064, "info", TransactionCategoryReference.Subscriptions),
        ["charity"] = new("charity", "Charity", "Donations, tithes, and community giving.", 0xea70, "success", TransactionCategoryReference.Charity),
        ["fitness"] = new("fitness", "Fitness", "Gym, sports gear, and workout classes.", 0xeb43, "warning", TransactionCategoryReference.Fitness),
        ["pets"] = new("pets", "Pets", "Food, vet visits, and supplies for your pets.", 0xe535, "accent", TransactionCategoryReference.Pets),
        ["investments"] = new("investments", "Investments", "Stocks, crypto, and other investment contributions.", 0xe8e5, "success", TransactionCategoryReference.Investments),
    };

    public static BudgetCategoryTemplate? GetById(string id) =>
        Templates.GetValueOrDefault(id);
}
