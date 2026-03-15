using Aonik.Finance.Contracts.Models.PersonalFinance;

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
            new(FinancialLifeGraphNodeTypes.UserRoot, false, true),
            new(FinancialLifeGraphNodeTypes.Household, false, true),
            new(FinancialLifeGraphNodeTypes.HouseholdMember, false, true),
            new(FinancialLifeGraphNodeTypes.Party, false, true),
            new(FinancialLifeGraphNodeTypes.PersonalAccount, false, true),
            new(FinancialLifeGraphNodeTypes.FinancialLinkedAccount, false, true),
            new(FinancialLifeGraphNodeTypes.PersonalTransaction, false, true),
            new(FinancialLifeGraphNodeTypes.Bill, false, true),
            new(FinancialLifeGraphNodeTypes.Goal, false, true),
            new(FinancialLifeGraphNodeTypes.Subscription, false, true),
            new(FinancialLifeGraphNodeTypes.FxQuote, false, true),
            new(FinancialLifeGraphNodeTypes.OrderRef, false, true),
            new(FinancialLifeGraphNodeTypes.InvoiceRef, false, true),
            new(FinancialLifeGraphNodeTypes.PaymentIntentRef, false, true),
            new(FinancialLifeGraphNodeTypes.NativeAnnotation, true, false),
            new(FinancialLifeGraphNodeTypes.RelationshipAnnotation, true, false),
            new(FinancialLifeGraphNodeTypes.InferredAnnotation, true, false)
        };

        return items.ToDictionary(item => item.NodeType, item => item, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FinancialLifeGraphEdgeDefinition> BuildEdges()
    {
        var edges = new List<FinancialLifeGraphEdgeDefinition>
        {
            new(FinancialLifeGraphPredicates.OwnsAccount, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.PersonalAccount, false),
            new(FinancialLifeGraphPredicates.HasTransaction, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.PersonalTransaction, false),
            new(FinancialLifeGraphPredicates.HasTransaction, FinancialLifeGraphNodeTypes.PersonalAccount, FinancialLifeGraphNodeTypes.PersonalTransaction, false),
            new(FinancialLifeGraphPredicates.UsesAccount, FinancialLifeGraphNodeTypes.PersonalTransaction, FinancialLifeGraphNodeTypes.PersonalAccount, false),
            new(FinancialLifeGraphPredicates.UsesLinkedAccount, FinancialLifeGraphNodeTypes.PersonalAccount, FinancialLifeGraphNodeTypes.FinancialLinkedAccount, false),
            new(FinancialLifeGraphPredicates.HasBill, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Bill, false),
            new(FinancialLifeGraphPredicates.HasGoal, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Goal, false),
            new(FinancialLifeGraphPredicates.HasSubscription, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Subscription, false),
            new(FinancialLifeGraphPredicates.BelongsToHousehold, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Household, false),
            new(FinancialLifeGraphPredicates.HouseholdHasMember, FinancialLifeGraphNodeTypes.Household, FinancialLifeGraphNodeTypes.HouseholdMember, false),
            new(FinancialLifeGraphPredicates.RelatedToParty, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.Party, true),
            new(FinancialLifeGraphPredicates.LinkedToOrder, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.OrderRef, false),
            new(FinancialLifeGraphPredicates.LinkedToInvoice, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.InvoiceRef, false),
            new(FinancialLifeGraphPredicates.LinkedToPaymentIntent, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.PaymentIntentRef, false),
            new(FinancialLifeGraphPredicates.FundedByAccount, FinancialLifeGraphNodeTypes.Goal, FinancialLifeGraphNodeTypes.PersonalAccount, true),
            new(FinancialLifeGraphPredicates.FundedByAccount, FinancialLifeGraphNodeTypes.Bill, FinancialLifeGraphNodeTypes.PersonalAccount, true),
            new(FinancialLifeGraphPredicates.HasFxContext, FinancialLifeGraphNodeTypes.UserRoot, FinancialLifeGraphNodeTypes.FxQuote, false)
        };

        var annotatableTypes = new[]
        {
            FinancialLifeGraphNodeTypes.UserRoot,
            FinancialLifeGraphNodeTypes.Household,
            FinancialLifeGraphNodeTypes.HouseholdMember,
            FinancialLifeGraphNodeTypes.Party,
            FinancialLifeGraphNodeTypes.PersonalAccount,
            FinancialLifeGraphNodeTypes.FinancialLinkedAccount,
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
                edges.Add(new FinancialLifeGraphEdgeDefinition(FinancialLifeGraphPredicates.AnnotatedAs, fromNodeType, toNodeType, true));
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
