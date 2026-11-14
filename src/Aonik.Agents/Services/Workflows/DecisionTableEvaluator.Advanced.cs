using System.Globalization;
using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal static partial class DecisionTableEvaluator
{
    private enum Truth { False, True, Unknown }
    private const int MaximumPredicateDepth = 4;
    private const int MaximumPredicateNodes = 100;
    internal static bool ValidFactValue(string type, JsonElement value) => IsValidValue(type, value);

    internal static IReadOnlyList<string> Routes(DecisionTable table) => table.HitPolicy switch
    {
        "Collect" => [.. new[] { table.CollectRoute!, table.DefaultRoute }.Distinct(StringComparer.Ordinal)],
        "Score" => [.. (table.ScoreBands ?? []).Select(band => band.Route).Append(table.DefaultRoute).Distinct(StringComparer.Ordinal)],
        _ => [.. table.Rows.Select(row => row.Route).Append(table.DefaultRoute).Distinct(StringComparer.Ordinal)],
    };

    private static IReadOnlyList<WorkflowDiagnostic> ValidateAdvanced(DecisionTable table, string path)
    {
        var errors = new List<WorkflowDiagnostic>();
        if (table.FactSchema != InvoiceSchema || table.HitPolicy is not ("FirstMatch" or "Unique" or "Collect" or "Score")
            || table.OptionalFactBehavior != "Unknown")
            errors.Add(new("DecisionSchema", path, "Use schema 2, invoice-approval.v1, a supported hit policy and explicit Unknown optional-fact behavior."));
        if (!WorkflowDefinitionCodec.IsKey(table.DefaultRoute)) errors.Add(new("DefaultRouteRequired", path, "A default route is required."));
        if (table.Rows is null || table.Rows.Count > 50 || table.Fixtures?.Count > 50)
            return [.. errors, new("DecisionRows", path, "Use at most 50 rows and 50 fixtures.")];
        int nodes = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal); var priorities = new HashSet<int>();
        var comparisons = new List<DecisionComparison>();
        decimal minimumScore = 0, maximumScore = 0;
        foreach (DecisionRow row in table.Rows)
        {
            if (row is null || !WorkflowDefinitionCodec.IsKey(row.Id) || !ids.Add(row.Id))
            { errors.Add(new("DuplicateRow", path, "Row IDs must be nonempty and unique.")); continue; }
            string rowPath = $"{path}.rows[{row.Id}]";
            if (!priorities.Add(row.Priority)) errors.Add(new("DuplicatePriority", rowPath, "Priorities must be unique."));
            if (!WorkflowDefinitionCodec.IsKey(row.Route)) errors.Add(new("RouteRequired", rowPath, "A valid route or collected item key is required."));
            if (row.Condition is not null)
            {
                if (row.All is null || row.All.Count != 0) errors.Add(new("PredicateChoice", rowPath, "Use a condition or flat all comparisons, never both."));
                Visit(row.Condition, 1, rowPath);
            }
            else if (row.All is null || row.All.Count is < 1 or > 8)
                errors.Add(new("ComparisonsRequired", rowPath, "Use one to eight flat comparisons or a bounded predicate group."));
            else
            {
                nodes++; foreach (DecisionComparison comparison in row.All) { nodes++; CheckComparison(comparison, rowPath); }
            }
            if (table.HitPolicy == "Score")
            {
                if (!BoundedDecimal(row.ScoreWeight, 10_000, out decimal weight)) errors.Add(new("ScoreWeight", rowPath, "Score weights are invariant decimal strings, at most four decimal places and magnitude 10000."));
                else { minimumScore += Math.Min(0, weight); maximumScore += Math.Max(0, weight); }
            }
            else if (row.ScoreWeight is not null) errors.Add(new("UnexpectedScore", rowPath, "Weights are only valid for Score."));
            if (table.HitPolicy == "Collect")
            {
                if (row.Route is not { Length: <= 32 } || !ValidCollectBinding(row.CollectInvoiceId))
                    errors.Add(new("CollectItem", rowPath, "Collect declares a stable key up to 32 characters and one UUID literal or declared invoice ID fact."));
            }
            else if (row.CollectInvoiceId is not null) errors.Add(new("UnexpectedCollection", rowPath, "Collected items are only valid for Collect."));
        }
        if (nodes > MaximumPredicateNodes) errors.Add(new("PredicateLimit", path, "A table permits at most 100 predicate nodes including groups."));
        bool usesAmount = comparisons.Any(comparison => comparison.Field == "invoice.total");
        if ((usesAmount || table.HitPolicy == "Score") && (table.Currency is null || !IsValidValue("Currency", JsonSerializer.SerializeToElement(table.Currency))))
            errors.Add(new("CurrencyRequired", path, "Amount and Score tables must explicitly select one invoice currency."));
        if (table.Currency is not null && !IsValidValue("Currency", JsonSerializer.SerializeToElement(table.Currency)))
            errors.Add(new("ComparisonType", path, "Currency is an uppercase three-letter code."));
        if (table.HitPolicy == "Collect" && (!WorkflowDefinitionCodec.IsKey(table.CollectRoute) || table.CollectRoute == table.DefaultRoute))
            errors.Add(new("CollectRoute", path, "Declare a collection consumer route distinct from the no-match default."));
        if (table.HitPolicy != "Collect" && table.CollectRoute is not null) errors.Add(new("UnexpectedCollection", path, "A collection route is only valid for Collect."));
        if (table.HitPolicy == "Score") ValidateScoreBands();
        else if (table.ScoreBands is not null) errors.Add(new("UnexpectedScore", path, "Score bands are only valid for Score."));
        return errors;

        void Visit(DecisionPredicate? predicate, int depth, string rowPath)
        {
            nodes++;
            if (predicate is null || depth > MaximumPredicateDepth || nodes > MaximumPredicateNodes)
            { errors.Add(new("PredicateLimit", rowPath, "Predicate depth is at most four, with at most 100 nodes per table.")); return; }
            if (predicate.Kind == "Comparison")
            {
                if (predicate.Children is { Count: > 0 }) errors.Add(new("PredicateShape", rowPath, "A comparison cannot have child predicates."));
                CheckComparison(predicate.Comparison, rowPath); return;
            }
            if (predicate.Kind is not ("All" or "Any" or "Not") || predicate.Comparison is not null
                || predicate.Children is null || predicate.Children.Count is < 1 or > MaximumPredicateNodes
                || (predicate.Kind == "Not" && predicate.Children.Count != 1))
            { errors.Add(new("PredicateShape", rowPath, "Use nonempty All/Any groups or exactly one Not child.")); return; }
            foreach (DecisionPredicate child in predicate.Children) Visit(child, depth + 1, rowPath);
        }
        void CheckComparison(DecisionComparison? comparison, string rowPath)
        {
            if (comparison is null || !InvoiceFields.TryGetValue(comparison.Field ?? "", out string? type) || !Operators.Contains(comparison.Operator ?? ""))
            { errors.Add(new("UnknownComparison", rowPath, "Use a declared invoice field and supported operator.")); return; }
            comparisons.Add(comparison);
            if (comparison.Operator == "isMissing") return;
            if (comparison.Value is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            { errors.Add(new("ComparisonValue", rowPath, "A typed comparison value is required.")); return; }
            bool valid = comparison.Operator == "in" ? value.ValueKind == JsonValueKind.Array && value.GetArrayLength() is > 0 and <= 100
                && value.EnumerateArray().All(item => IsValidValue(type, item)) : IsValidValue(type, value);
            if (!valid) errors.Add(new("ComparisonType", rowPath, "Comparison values must match their declared fact type; decimal amounts use invariant strings."));
            if (comparison.Operator is "gt" or "gte" or "lt" or "lte" && type != "Decimal")
                errors.Add(new("ComparisonOperator", rowPath, "Ordered comparisons require a decimal fact."));
        }
        void ValidateScoreBands()
        {
            if (table.ScoreBands is null || table.ScoreBands.Count is < 1 or > 50)
            { errors.Add(new("ScoreBands", path, "Declare 1–50 non-overlapping score bands covering every possible score.")); return; }
            decimal? previous = null;
            foreach (DecisionScoreBand band in table.ScoreBands)
            {
                if (band is null || !BoundedDecimal(band.MinimumInclusive, 500_001, out decimal lower)
                    || !BoundedDecimal(band.MaximumExclusive, 500_001, out decimal upper) || lower >= upper || !WorkflowDefinitionCodec.IsKey(band.Route))
                { errors.Add(new("ScoreBands", path, "Bands need ordered decimal bounds and a valid route.")); continue; }
                if ((previous is null && lower > minimumScore) || (previous is { } end && end != lower))
                    errors.Add(new("ScoreBands", path, "Score bands must be sorted, adjacent and non-overlapping."));
                previous = upper;
            }
            if (previous is null || previous <= maximumScore) errors.Add(new("ScoreBands", path, "Score bands must cover the maximum possible score."));
        }
    }

    internal static DecisionEvaluation EvaluateAdvanced(DecisionTable table, IReadOnlyDictionary<string, JsonElement> facts,
        IReadOnlyList<DecisionFactField>? declaredInputs = null)
    {
        IReadOnlyList<WorkflowDiagnostic> errors = ValidateAdvanced(table, "decision");
        if (errors.Any(error => error.Severity == "Error")) return new("Blocked", [], [], [], errors.First().Message);
        IEnumerable<string> required = declaredInputs is null ? RequiredInvoiceFields : declaredInputs.Where(field => field.Required).Select(field => field.Name);
        foreach (string field in required)
            if (!facts.TryGetValue(field, out JsonElement value) || IsMissing(value)) return new("NeedsManualReview", [], [], [], $"Required fact {field} is missing.");
        foreach ((string field, JsonElement value) in facts)
            if (!InvoiceFields.TryGetValue(field, out string? type) || (!IsMissing(value) && !IsValidValue(type, value)))
                return new("NeedsManualReview", [], [], [], $"Fact {field} is not valid for {InvoiceSchema}.");
        if (table.Currency is { } currency && (!facts.TryGetValue("invoice.currency", out JsonElement actualCurrency)
            || actualCurrency.ValueKind != JsonValueKind.String || actualCurrency.GetString() != currency))
            return new("NeedsManualReview", [], [], [], "InvoiceCurrencyMismatch");
        var trace = new List<DecisionRowTrace>(); var matched = new List<DecisionRow>(); bool unknown = false;
        foreach (DecisionRow row in table.Rows.OrderBy(row => row.Priority))
        {
            DecisionPredicate predicate = row.Condition ?? new("All", Children: row.All.Select(comparison => new DecisionPredicate("Comparison", comparison)).ToList());
            DecisionPredicateTrace evidence = EvaluatePredicate(predicate);
            Truth state = Enum.Parse<Truth>(evidence.State);
            var flat = new List<DecisionComparisonTrace>(); Flatten(evidence, flat);
            string? contribution = table.HitPolicy == "Score" ? DecimalString(state == Truth.True ? decimal.Parse(row.ScoreWeight!, CultureInfo.InvariantCulture) : 0) : null;
            trace.Add(new(row.Id, state == Truth.True, flat, evidence, state.ToString(), contribution));
            if (state == Truth.Unknown)
            {
                unknown = true;
                if (table.HitPolicy == "FirstMatch") return new("NeedsManualReview", [], [], trace, "UnknownPredicate");
            }
            if (state != Truth.True) continue;
            matched.Add(row);
            if (table.HitPolicy == "FirstMatch") return new("Completed", [row.Route], [row.Id], trace, "FirstMatchingRow");
        }
        if (unknown) return new("NeedsManualReview", [], matched.Select(row => row.Id).ToList(), trace, "UnknownPredicate");
        if (table.HitPolicy == "Unique" && matched.Count > 1) return new("NeedsManualReview", [], matched.Select(row => row.Id).ToList(), trace, "UniqueConflict");
        if (table.HitPolicy == "Collect")
        {
            var items = new List<WorkflowLoopItem>();
            foreach (DecisionRow row in matched)
            {
                WorkflowInputBinding binding = row.CollectInvoiceId!;
                JsonElement? value = binding.Literal ?? (facts.TryGetValue(binding.Fact!, out JsonElement fact) ? fact : null);
                if (value is not { ValueKind: JsonValueKind.String } id || !id.TryGetGuid(out Guid invoiceId) || invoiceId == Guid.Empty)
                    return new("NeedsManualReview", [], matched.Select(row => row.Id).ToList(), trace, "CollectedInvoiceMissing");
                WorkflowLoopItem? duplicate = items.SingleOrDefault(item => item.Key == row.Route);
                if (duplicate is not null && duplicate.InvoiceId != invoiceId) return new("NeedsManualReview", [], matched.Select(row => row.Id).ToList(), trace, "CollectionKeyConflict");
                if (duplicate is null) items.Add(new(row.Route, invoiceId));
            }
            return new("Completed", [items.Count == 0 ? table.DefaultRoute : table.CollectRoute!], matched.Select(row => row.Id).ToList(), trace,
                items.Count == 0 ? "DefaultRoute" : "CollectedInvoiceReferences", items);
        }
        if (table.HitPolicy == "Score")
        {
            decimal total = matched.Sum(row => decimal.Parse(row.ScoreWeight!, CultureInfo.InvariantCulture));
            DecisionScoreBand? band = table.ScoreBands!.SingleOrDefault(band => total >= decimal.Parse(band.MinimumInclusive, CultureInfo.InvariantCulture)
                && total < decimal.Parse(band.MaximumExclusive, CultureInfo.InvariantCulture));
            return band is null ? new("NeedsManualReview", [], matched.Select(row => row.Id).ToList(), trace, "ScoreOutsideBands", ScoreTotal: DecimalString(total))
                : new("Completed", [band.Route], matched.Select(row => row.Id).ToList(), trace, "ScoreBand", ScoreTotal: DecimalString(total));
        }
        return new("Completed", [matched.Count == 0 ? table.DefaultRoute : matched[0].Route], matched.Select(row => row.Id).ToList(), trace,
            matched.Count == 0 ? "DefaultRoute" : "UniqueMatchingRow");

        DecisionPredicateTrace EvaluatePredicate(DecisionPredicate predicate)
        {
            if (predicate.Kind == "Comparison")
            {
                DecisionComparison comparison = predicate.Comparison!;
                bool present = facts.TryGetValue(comparison.Field, out JsonElement actual) && !IsMissing(actual);
                Truth state = comparison.Operator == "isMissing" ? present ? Truth.False : Truth.True
                    : !present ? Truth.Unknown : Matches(comparison, actual) ? Truth.True : Truth.False;
                return new("Comparison", state.ToString(), new(comparison.Field, comparison.Operator, state == Truth.True,
                    present ? actual.Clone() : null, comparison.Value?.Clone(), state.ToString()));
            }
            List<DecisionPredicateTrace> children = predicate.Children!.Select(EvaluatePredicate).ToList();
            Truth stateValue = predicate.Kind switch
            {
                "Not" => children[0].State == "Unknown" ? Truth.Unknown : children[0].State == "True" ? Truth.False : Truth.True,
                "All" => children.Any(child => child.State == "False") ? Truth.False : children.Any(child => child.State == "Unknown") ? Truth.Unknown : Truth.True,
                _ => children.Any(child => child.State == "True") ? Truth.True : children.Any(child => child.State == "Unknown") ? Truth.Unknown : Truth.False,
            };
            return new(predicate.Kind, stateValue.ToString(), Children: children);
        }
    }

    private static void Flatten(DecisionPredicateTrace predicate, List<DecisionComparisonTrace> comparisons)
    {
        if (predicate.Comparison is { } comparison) comparisons.Add(comparison);
        foreach (DecisionPredicateTrace child in predicate.Children ?? []) Flatten(child, comparisons);
    }
    internal static bool ValidCollectBinding(WorkflowInputBinding? binding) => binding is not null && binding.Step is null && binding.Output is null
        && (binding.Fact is null && binding.Literal is { ValueKind: JsonValueKind.String } literal && literal.TryGetGuid(out Guid id) && id != Guid.Empty
            || binding.Literal is null && binding.Fact == "invoice.id");
    private static string DecimalString(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static bool BoundedDecimal(string? value, decimal maximum, out decimal number)
    {
        number = 0;
        int dot = value?.IndexOf('.') ?? -1;
        return value is { Length: > 0 and <= 32 } && (dot < 0 || value.Length - dot - 1 <= 4)
            && decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number)
            && number >= -maximum && number <= maximum;
    }
}
