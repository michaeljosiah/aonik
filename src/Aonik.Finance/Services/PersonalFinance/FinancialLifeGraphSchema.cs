namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphSchema
{
    private readonly IReadOnlyDictionary<string, FinancialLifeGraphNodeTypeDefinition> _nodeTypes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<FinancialLifeGraphEdgeDefinition>> _edgesByPredicate;

    public FinancialLifeGraphSchema()
    {
        _nodeTypes = BuildNodeTypes();
        _edgesByPredicate = BuildEdges()
            .GroupBy(item => item.Predicate, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FinancialLifeGraphEdgeDefinition>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, FinancialLifeGraphNodeTypeDefinition> NodeTypes => _nodeTypes;

    public IReadOnlyCollection<string> Predicates => _edgesByPredicate.Keys.ToList();

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

    private static IReadOnlyDictionary<string, FinancialLifeGraphNodeTypeDefinition> BuildNodeTypes()
    {
        var items = new List<FinancialLifeGraphNodeTypeDefinition>
        {
            new("UserRoot", false, true),
            new("Household", false, true),
            new("HouseholdMember", false, true),
            new("Party", false, true),
            new("PersonalAccount", false, true),
            new("FinancialLinkedAccount", false, true),
            new("PersonalTransaction", false, true),
            new("Bill", false, true),
            new("Goal", false, true),
            new("Subscription", false, true),
            new("FxQuote", false, true),
            new("OrderRef", false, true),
            new("InvoiceRef", false, true),
            new("PaymentIntentRef", false, true),
            new("NativeAnnotation", true, false),
            new("RelationshipAnnotation", true, false),
            new("InferredAnnotation", true, false)
        };

        return items.ToDictionary(item => item.NodeType, item => item, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FinancialLifeGraphEdgeDefinition> BuildEdges()
    {
        var edges = new List<FinancialLifeGraphEdgeDefinition>
        {
            new("OWNS_ACCOUNT", "UserRoot", "PersonalAccount", false),
            new("HAS_TRANSACTION", "UserRoot", "PersonalTransaction", false),
            new("HAS_TRANSACTION", "PersonalAccount", "PersonalTransaction", false),
            new("USES_ACCOUNT", "PersonalTransaction", "PersonalAccount", false),
            new("USES_ACCOUNT", "PersonalAccount", "FinancialLinkedAccount", false),
            new("USES_LINKED_ACCOUNT", "PersonalAccount", "FinancialLinkedAccount", false),
            new("HAS_BILL", "UserRoot", "Bill", false),
            new("HAS_GOAL", "UserRoot", "Goal", false),
            new("HAS_SUBSCRIPTION", "UserRoot", "Subscription", false),
            new("BELONGS_TO_HOUSEHOLD", "UserRoot", "Household", false),
            new("HOUSEHOLD_HAS_MEMBER", "Household", "HouseholdMember", false),
            new("RELATED_TO_PARTY", "UserRoot", "Party", true),
            new("LINKED_TO_ORDER", "Bill", "OrderRef", false),
            new("LINKED_TO_INVOICE", "Bill", "InvoiceRef", false),
            new("LINKED_TO_PAYMENT_INTENT", "Bill", "PaymentIntentRef", false),
            new("FUNDED_BY_ACCOUNT", "Goal", "PersonalAccount", true),
            new("FUNDED_BY_ACCOUNT", "Bill", "PersonalAccount", true),
            new("HAS_FX_CONTEXT", "UserRoot", "FxQuote", false)
        };

        var annotatableTypes = new[]
        {
            "UserRoot",
            "Household",
            "HouseholdMember",
            "Party",
            "PersonalAccount",
            "FinancialLinkedAccount",
            "PersonalTransaction",
            "Bill",
            "Goal",
            "Subscription"
        };

        var annotationTypes = new[]
        {
            "NativeAnnotation",
            "RelationshipAnnotation",
            "InferredAnnotation"
        };

        foreach (var fromNodeType in annotatableTypes)
        {
            foreach (var toNodeType in annotationTypes)
            {
                edges.Add(new FinancialLifeGraphEdgeDefinition("ANNOTATED_AS", fromNodeType, toNodeType, true));
            }
        }

        return edges;
    }
}

internal sealed record FinancialLifeGraphNodeTypeDefinition(
    string NodeType,
    bool CanBeCreatedNatively,
    bool IsMirrorProjection);

internal sealed record FinancialLifeGraphEdgeDefinition(
    string Predicate,
    string FromNodeType,
    string ToNodeType,
    bool CanBeCreatedNatively);
