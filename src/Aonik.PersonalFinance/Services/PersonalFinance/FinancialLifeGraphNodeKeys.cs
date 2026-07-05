namespace Aonik.PersonalFinance.Services;

internal static class FinancialLifeGraphNodeKeys
{
    public const string User = "user";
    public const string Household = "household";
    public const string HouseholdMember = "household-member";
    public const string Party = "party";
    public const string PersonalAccount = "personal-account";
    public const string LinkedAccount = "linked-account";
    public const string PersonalTransaction = "personal-transaction";
    public const string Bill = "bill";
    public const string Goal = "goal";
    public const string Subscription = "subscription";
    public const string FxQuote = "fx-quote";
    public const string NativeNode = "native-node";
    public const string OrderRef = "order-ref";
    public const string InvoiceRef = "invoice-ref";
    public const string PaymentIntentRef = "payment-intent-ref";

    public static string Build(string prefix, Guid id) => $"{prefix}:{id:D}";

    public static bool TryParse(string nodeKey, out string prefix, out Guid nodeId)
    {
        prefix = string.Empty;
        nodeId = Guid.Empty;

        var parts = nodeKey.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out nodeId))
        {
            return false;
        }

        prefix = parts[0].Trim().ToLowerInvariant();
        return true;
    }
}
