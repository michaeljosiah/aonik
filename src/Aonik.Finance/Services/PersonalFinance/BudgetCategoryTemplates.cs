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
        ["housing"] = new("housing", "Housing", "Track rent, repairs, and household supplies.", 0xf085, "primary", "finances"),
        ["groceries"] = new("groceries", "Food & Groceries", "Supermarket runs, fresh market, and coffee stops.", 0xe56b, "success", "groceries"),
        ["transport"] = new("transport", "Transport", "Fuel, ride apps, and public transit fares.", 0xe1d1, "warning", "transport"),
        ["utilities"] = new("utilities", "Utilities", "Electricity, water, and internet bills.", 0xe90f, "info", "finances"),
        ["personal"] = new("personal", "Personal Care", "Grooming, pharmacy, and gym memberships.", 0xeb4c, "accent", "shopping"),
        ["eating-out"] = new("eating-out", "Eating Out", "Restaurants, takeaways, and dining with friends.", 0xe56c, "warning", "groceries"),
        ["shopping"] = new("shopping", "Shopping", "Clothing, electronics, and everyday purchases.", 0xf37a, "accent", "shopping"),
        ["entertainment"] = new("entertainment", "Entertainment", "Movies, streaming, games, and nights out.", 0xe02c, "primary", "entertainment"),
        ["bills"] = new("bills", "Bills", "Phone, insurance, and recurring monthly bills.", 0xef6b, "danger", "finances"),
        ["health"] = new("health", "Health", "Doctor visits, prescriptions, and wellness.", 0xe87e, "danger", "shopping"),
        ["education"] = new("education", "Education", "Tuition, books, courses, and learning materials.", 0xe80c, "info", "finances"),
        ["gifts"] = new("gifts", "Gifts", "Birthday, holiday, and special occasion presents.", 0xe8f6, "accent", "shopping"),
        ["travel"] = new("travel", "Travel", "Flights, hotels, and holiday spending.", 0xe539, "primary", "transport"),
        ["savings"] = new("savings", "Savings", "Emergency fund, rainy day, and saving goals.", 0xf14f, "success", "finances"),
        ["subscriptions"] = new("subscriptions", "Subscriptions", "Streaming, apps, memberships, and recurring charges.", 0xe064, "info", "entertainment"),
        ["charity"] = new("charity", "Charity", "Donations, tithes, and community giving.", 0xea70, "success", "finances"),
        ["fitness"] = new("fitness", "Fitness", "Gym, sports gear, and workout classes.", 0xeb43, "warning", "shopping"),
        ["pets"] = new("pets", "Pets", "Food, vet visits, and supplies for your pets.", 0xe535, "accent", "shopping"),
        ["investments"] = new("investments", "Investments", "Stocks, crypto, and other investment contributions.", 0xe8e5, "success", "finances"),
    };

    public static BudgetCategoryTemplate? GetById(string id) =>
        Templates.GetValueOrDefault(id);
}
