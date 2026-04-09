using Aonik.Finance.Contracts.Models.PersonalFinance;
using System.Text;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphSchema
{
    private readonly IReadOnlyDictionary<string, FinancialLifeGraphNodeTypeDefinition> _nodeTypes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<FinancialLifeGraphEdgeDefinition>> _edgesByPredicate;
    private readonly IReadOnlyList<FinancialLifeGraphEdgeDefinition> _allEdges;

    public FinancialLifeGraphSchema()
    {
        _nodeTypes = BuildNodeTypes();
        _allEdges = BuildEdges();
        _edgesByPredicate = _allEdges
            .GroupBy(item => item.Predicate, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FinancialLifeGraphEdgeDefinition>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, FinancialLifeGraphNodeTypeDefinition> NodeTypes => _nodeTypes;

    public IReadOnlyCollection<string> Predicates => _edgesByPredicate.Keys.ToList();

    public IReadOnlyList<FinancialLifeGraphEdgeDefinition> AllEdges => _allEdges;

    public bool TryGetNodeType(string nodeType, out FinancialLifeGraphNodeTypeDefinition? definition)
    {
        return _nodeTypes.TryGetValue(nodeType.Trim(), out definition);
    }

    public bool IsKnownPredicate(string predicate)
    {
        return _edgesByPredicate.ContainsKey(predicate.Trim());
    }

    public bool CanCreateNodeNatively(string nodeType)
    {
        return TryGetNodeType(nodeType, out var definition) && definition!.CanBeCreatedNatively;
    }

    public bool IsAllowedEdge(
        string fromNodeType,
        string predicate,
        string toNodeType,
        bool requireNativeCreatable)
    {
        if (!_edgesByPredicate.TryGetValue(predicate.Trim(), out var definitions))
        {
            return false;
        }

        return definitions.Any(item =>
            item.FromNodeType.Equals(fromNodeType.Trim(), StringComparison.OrdinalIgnoreCase)
            && item.ToNodeType.Equals(toNodeType.Trim(), StringComparison.OrdinalIgnoreCase)
            && (!requireNativeCreatable || item.CanBeCreatedNatively));
    }

    /// <summary>
    /// Returns all edge definitions where the given node type appears as the source (From).
    /// </summary>
    public IReadOnlyList<FinancialLifeGraphEdgeDefinition> GetOutboundEdges(string nodeType)
    {
        return _allEdges
            .Where(item => item.FromNodeType.Equals(nodeType.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns all edge definitions where the given node type appears as the target (To).
    /// </summary>
    public IReadOnlyList<FinancialLifeGraphEdgeDefinition> GetInboundEdges(string nodeType)
    {
        return _allEdges
            .Where(item => item.ToNodeType.Equals(nodeType.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Generates a compact text summary of the schema suitable for injection into an agent system prompt.
    /// </summary>
    public string GenerateCompactSchemaPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Financial Life Graph Schema");
        sb.AppendLine();
        sb.AppendLine("### Node Types");
        foreach (var nt in _nodeTypes.Values.OrderBy(item => item.NodeType))
        {
            var origin = nt.IsMirrorProjection ? "projected" : "native";
            sb.AppendLine($"- **{nt.NodeType}** ({origin}): {nt.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("### Predicates (Edges)");
        foreach (var group in _edgesByPredicate.OrderBy(item => item.Key))
        {
            var first = group.Value[0];
            sb.AppendLine($"- **{group.Key}**: {first.ReasoningHint}");
            foreach (var edge in group.Value)
            {
                sb.AppendLine($"  - {edge.FromNodeType} -> {edge.ToNodeType}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("### Node Key Format");
        sb.AppendLine("All node keys follow the pattern `{prefix}:{guid}`. Use the node key as the anchor for traversal and retrieval tool calls.");

        return sb.ToString();
    }

    private static IReadOnlyDictionary<string, FinancialLifeGraphNodeTypeDefinition> BuildNodeTypes()
    {
        var items = new List<FinancialLifeGraphNodeTypeDefinition>
        {
            new(FinancialLifeGraphNodeTypes.UserRoot, false, true,
                "The authenticated user — root of the entire financial graph. Start traversal here to explore any aspect of the user's financial life."),
            new(FinancialLifeGraphNodeTypes.Household, false, true,
                "A household grouping that links the user to shared household members. Follow this path to understand shared financial context and family structure."),
            new(FinancialLifeGraphNodeTypes.HouseholdMember, false, true,
                "An individual member within a household. Contains role and permissions within the household context."),
            new(FinancialLifeGraphNodeTypes.Party, false, true,
                "A related party (person or business) connected to the user through a defined relationship. Follow this to understand who the user sends money to, receives money from, or has financial obligations with."),
            new(FinancialLifeGraphNodeTypes.PersonalAccount, false, true,
                "A user-owned financial account (checking, savings, etc.). This is a key hub node — accounts connect to transactions, bills, goals, and linked external accounts."),
            new(FinancialLifeGraphNodeTypes.PersonalLinkedAccount, false, true,
                "An external bank account connected through an aggregation provider (e.g., Plaid). Contains sync status and provider metadata."),
            new(FinancialLifeGraphNodeTypes.PersonalTransaction, false, true,
                "A financial transaction within the 120-day snapshot window. Contains amount, merchant, category, and classification data. The most granular financial data point in the graph."),
            new(FinancialLifeGraphNodeTypes.Bill, false, true,
                "A recurring bill or payment obligation. Contains payee, expected amount, due date, frequency, and autopay status. Follow FUNDED_BY_ACCOUNT to see which account pays it, or LINKED_TO_ORDER/INVOICE for payment execution details."),
            new(FinancialLifeGraphNodeTypes.Goal, false, true,
                "A savings or financial target. Contains target amount, progress, target date, and funding account. Useful for understanding the user's financial aspirations and progress."),
            new(FinancialLifeGraphNodeTypes.Subscription, false, true,
                "A recurring subscription detected from transaction patterns. Contains merchant, expected amount, and renewal date. Represents ongoing financial commitments."),
            new(FinancialLifeGraphNodeTypes.FxQuote, false, true,
                "An FX rate quote relevant to the user's account currencies. Useful for cross-currency analysis when the user holds accounts in multiple currencies."),
            new(FinancialLifeGraphNodeTypes.OrderRef, false, true,
                "A cross-reference to a business order linked to a bill. Contains order type, status, and amount details. Follow this to understand the business intent behind a payment."),
            new(FinancialLifeGraphNodeTypes.InvoiceRef, false, true,
                "A cross-reference to an invoice linked to a bill. Contains invoice status, amount, and due date. Follow this to understand billing details for an obligation."),
            new(FinancialLifeGraphNodeTypes.PaymentIntentRef, false, true,
                "A cross-reference to a payment intent linked to bill execution. Contains payment status, amount, and purpose. Follow this to understand payment execution state."),
            new(FinancialLifeGraphNodeTypes.NativeAnnotation, true, false,
                "A user-created annotation attached to any graph node. Represents user-provided context, notes, or categorisation that enriches the financial graph."),
            new(FinancialLifeGraphNodeTypes.RelationshipAnnotation, true, false,
                "A user-created relationship annotation describing how two financial concepts are connected beyond the standard predicates."),
            new(FinancialLifeGraphNodeTypes.InferredAnnotation, true, false,
                "An AI-proposed annotation pending user approval. Represents AI-detected patterns (e.g., recurring merchants) that enrich the graph when approved.")
        };

        return items.ToDictionary(item => item.NodeType, item => item, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FinancialLifeGraphEdgeDefinition> BuildEdges()
    {
        var edges = new List<FinancialLifeGraphEdgeDefinition>
        {
            new(FinancialLifeGraphPredicates.OwnsAccount, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.PersonalAccount, false,
                "Follow this to find all accounts the user owns. This is typically the first hop when exploring a user's financial structure."),
            new(FinancialLifeGraphPredicates.HasTransaction, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.PersonalTransaction, false,
                "Follow this to find transactions not linked to a specific account (orphaned or manually added). For account-linked transactions, traverse via PersonalAccount instead."),
            new(FinancialLifeGraphPredicates.HasTransaction, FinancialLifeGraphNodeTypes.PersonalAccount, FinancialLifeGraphNodeTypes.PersonalTransaction, false,
                "Follow this to find all transactions for a specific account. This is the primary path for analysing spending, income, and cash flow per account."),
            new(FinancialLifeGraphPredicates.UsesAccount, FinancialLifeGraphNodeTypes.PersonalTransaction, FinancialLifeGraphNodeTypes.PersonalAccount, false,
                "Follow this reverse link from a transaction back to its account. Useful when starting from a transaction and needing account context."),
            new(FinancialLifeGraphPredicates.UsesLinkedAccount, FinancialLifeGraphNodeTypes.PersonalAccount, FinancialLifeGraphNodeTypes.PersonalLinkedAccount, false,
                "Follow this to find the external provider account (e.g., Plaid) linked to a personal account. Useful for understanding data source and sync status."),
            new(FinancialLifeGraphPredicates.HasBill, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Bill, false,
                "Follow this to find all bills and payment obligations the user tracks. Bills represent recurring outflows with known payees and due dates."),
            new(FinancialLifeGraphPredicates.HasGoal, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Goal, false,
                "Follow this to find all savings goals. Goals represent the user's financial aspirations — house deposits, emergency funds, travel, etc."),
            new(FinancialLifeGraphPredicates.HasSubscription, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Subscription, false,
                "Follow this to find all detected subscriptions. Subscriptions are recurring charges detected from transaction patterns — streaming services, memberships, etc."),
            new(FinancialLifeGraphPredicates.BelongsToHousehold, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Household, false,
                "Follow this to find the user's household. The household is the shared financial context for family members or cohabitants."),
            new(FinancialLifeGraphPredicates.HouseholdHasMember, FinancialLifeGraphNodeTypes.Household, FinancialLifeGraphNodeTypes.HouseholdMember, false,
                "Follow this to find all members within a household. Each member has a role and permissions within the shared financial context."),
            new(FinancialLifeGraphPredicates.HouseholdHasAccount, FinancialLifeGraphNodeTypes.Household, FinancialLifeGraphNodeTypes.PersonalAccount, false,
                "Follow this to find personal accounts that have been explicitly shared with the household. Ownership stays with the original user, but accepted household members can view the shared account context."),
            new(FinancialLifeGraphPredicates.RelatedToParty, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Party, true,
                "Follow this to find all parties the user has a financial relationship with — family members receiving remittances, businesses they pay, beneficiaries, etc. The edge metadata carries the relationship type."),
            new(FinancialLifeGraphPredicates.LinkedToOrder, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.OrderRef, false,
                "Follow this from a bill to see the business order that initiated its payment. Reveals the business intent behind the financial obligation."),
            new(FinancialLifeGraphPredicates.LinkedToInvoice, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.InvoiceRef, false,
                "Follow this from a bill to see the invoice details. Reveals billing amounts, due dates, and payment terms for the obligation."),
            new(FinancialLifeGraphPredicates.LinkedToPaymentIntent, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.PaymentIntentRef, false,
                "Follow this from a bill to see payment execution attempts. Reveals whether payments are pending, captured, or failed."),
            new(FinancialLifeGraphPredicates.FundedByAccount, FinancialLifeGraphNodeTypes.Goal, FinancialLifeGraphNodeTypes.PersonalAccount, true,
                "Follow this to find which account funds a specific goal. Useful for understanding cash allocation and how savings goals compete for the same funds."),
            new(FinancialLifeGraphPredicates.FundedByAccount, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.PersonalAccount, true,
                "Follow this to find which account pays a specific bill. Useful for understanding which accounts carry obligation loads and cash outflow patterns."),
            new(FinancialLifeGraphPredicates.HasFxContext, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.FxQuote, false,
                "Follow this to find relevant FX quotes for the user's currency pairs. Useful when the user holds multi-currency accounts or has cross-border obligations.")
        };

        var annotatableTypes = new[]
        {
            FinancialLifeGraphNodeTypes.UserRoot,
            FinancialLifeGraphNodeTypes.Household,
            FinancialLifeGraphNodeTypes.HouseholdMember,
            FinancialLifeGraphNodeTypes.Party,
            FinancialLifeGraphNodeTypes.PersonalAccount,
            FinancialLifeGraphNodeTypes.PersonalLinkedAccount,
            FinancialLifeGraphNodeTypes.PersonalTransaction,
            FinancialLifeGraphNodeTypes.Bill,
            FinancialLifeGraphNodeTypes.Goal,
            FinancialLifeGraphNodeTypes.Subscription
        };

        var annotationTypes = new[]
        {
            FinancialLifeGraphNodeTypes.NativeAnnotation,
            FinancialLifeGraphNodeTypes.RelationshipAnnotation,
            FinancialLifeGraphNodeTypes.InferredAnnotation
        };

        foreach (var fromNodeType in annotatableTypes)
        {
            foreach (var toNodeType in annotationTypes)
            {
                edges.Add(new FinancialLifeGraphEdgeDefinition(
                    FinancialLifeGraphPredicates.AnnotatedAs, fromNodeType, toNodeType, true,
                    "Follow this to find annotations (user-created or AI-inferred) attached to a node. Annotations carry contextual notes, labels, or AI-detected patterns that enrich the node's meaning."));
            }
        }

        return edges;
    }
}

internal sealed record FinancialLifeGraphNodeTypeDefinition(
    string NodeType,
    bool CanBeCreatedNatively,
    bool IsMirrorProjection,
    string Description);

internal sealed record FinancialLifeGraphEdgeDefinition(
    string Predicate,
    string FromNodeType,
    string ToNodeType,
    bool CanBeCreatedNatively,
    string ReasoningHint);
