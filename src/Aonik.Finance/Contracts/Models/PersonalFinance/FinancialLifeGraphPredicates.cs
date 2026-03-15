namespace Aonik.Finance.Contracts.Models.PersonalFinance;

/// <summary>
/// Canonical edge predicate names used by the Financial Life Graph.
/// These values describe how projected nodes relate to each other.
/// </summary>
public static class FinancialLifeGraphPredicates
{
    /// <summary>
    /// Connects the user root node to a user-owned personal account.
    /// </summary>
    public const string OwnsAccount = "OWNS_ACCOUNT";

    /// <summary>
    /// Connects a user or account node to a transaction it contains.
    /// </summary>
    public const string HasTransaction = "HAS_TRANSACTION";

    /// <summary>
    /// Connects a transaction node to the personal account used for that transaction.
    /// </summary>
    public const string UsesAccount = "USES_ACCOUNT";

    /// <summary>
    /// Connects a personal account to its linked external provider account.
    /// </summary>
    public const string UsesLinkedAccount = "USES_LINKED_ACCOUNT";

    /// <summary>
    /// Connects the user root node to a bill obligation.
    /// </summary>
    public const string HasBill = "HAS_BILL";

    /// <summary>
    /// Connects the user root node to a savings or target goal.
    /// </summary>
    public const string HasGoal = "HAS_GOAL";

    /// <summary>
    /// Connects the user root node to a recurring subscription.
    /// </summary>
    public const string HasSubscription = "HAS_SUBSCRIPTION";

    /// <summary>
    /// Connects the user root node to a household it belongs to.
    /// </summary>
    public const string BelongsToHousehold = "BELONGS_TO_HOUSEHOLD";

    /// <summary>
    /// Connects a household node to one of its members.
    /// </summary>
    public const string HouseholdHasMember = "HOUSEHOLD_HAS_MEMBER";

    /// <summary>
    /// Connects the user root node to a related party derived from Party relationships.
    /// </summary>
    public const string RelatedToParty = "RELATED_TO_PARTY";

    /// <summary>
    /// Connects a bill or goal to the personal account that funds it.
    /// </summary>
    public const string FundedByAccount = "FUNDED_BY_ACCOUNT";

    /// <summary>
    /// Connects the user root node to a user-relevant FX quote enrichment.
    /// </summary>
    public const string HasFxContext = "HAS_FX_CONTEXT";

    /// <summary>
    /// Connects a mirror-projected node to a graph-native annotation node.
    /// </summary>
    public const string AnnotatedAs = "ANNOTATED_AS";

    /// <summary>
    /// Reserved predicate linking a bill to an order reference node.
    /// </summary>
    public const string LinkedToOrder = "LINKED_TO_ORDER";

    /// <summary>
    /// Reserved predicate linking a bill to an invoice reference node.
    /// </summary>
    public const string LinkedToInvoice = "LINKED_TO_INVOICE";

    /// <summary>
    /// Reserved predicate linking a bill to a payment-intent reference node.
    /// </summary>
    public const string LinkedToPaymentIntent = "LINKED_TO_PAYMENT_INTENT";
}
