namespace Aonik.PersonalFinance.Contracts.Models;

/// <summary>
/// Canonical node type names used by the Financial Life Graph read model.
/// These values are serialized in API responses and persisted for native graph nodes.
/// </summary>
public static class FinancialLifeGraphNodeTypes
{
    /// <summary>
    /// The authenticated user root node for the current graph projection.
    /// </summary>
    public const string UserRoot = "UserRoot";

    /// <summary>
    /// A household grouping that links the current user to shared household members.
    /// </summary>
    public const string Household = "Household";

    /// <summary>
    /// A member inside a projected household.
    /// </summary>
    public const string HouseholdMember = "HouseholdMember";

    /// <summary>
    /// A related party derived from tenant-scoped Party and PartyRelationship data.
    /// </summary>
    public const string Party = "Party";

    /// <summary>
    /// A user-owned Personal Finance account.
    /// </summary>
    public const string PersonalAccount = "PersonalAccount";

    /// <summary>
    /// A linked account synchronized from an external aggregation provider.
    /// </summary>
    public const string PersonalLinkedAccount = "PersonalLinkedAccount";

    /// <summary>
    /// A personal finance transaction projected into the graph.
    /// </summary>
    public const string PersonalTransaction = "PersonalTransaction";

    /// <summary>
    /// A bill or payment obligation tracked in Personal Finance.
    /// </summary>
    public const string Bill = "Bill";

    /// <summary>
    /// A savings or target goal tracked in Personal Finance.
    /// </summary>
    public const string Goal = "Goal";

    /// <summary>
    /// A recurring subscription tracked in Personal Finance.
    /// </summary>
    public const string Subscription = "Subscription";

    /// <summary>
    /// An FX quote relevant to the current user's account currencies.
    /// </summary>
    public const string FxQuote = "FxQuote";

    /// <summary>
    /// Reserved reference node for an order linked to a graph concept.
    /// </summary>
    public const string OrderRef = "OrderRef";

    /// <summary>
    /// Reserved reference node for an invoice linked to a graph concept.
    /// </summary>
    public const string InvoiceRef = "InvoiceRef";

    /// <summary>
    /// Reserved reference node for a payment intent linked to a graph concept.
    /// </summary>
    public const string PaymentIntentRef = "PaymentIntentRef";

    /// <summary>
    /// A user-created graph-native annotation.
    /// </summary>
    public const string NativeAnnotation = "NativeAnnotation";

    /// <summary>
    /// A user-created graph-native relationship annotation.
    /// </summary>
    public const string RelationshipAnnotation = "RelationshipAnnotation";

    /// <summary>
    /// An AI-proposed annotation pending explicit approval.
    /// </summary>
    public const string InferredAnnotation = "InferredAnnotation";
}
