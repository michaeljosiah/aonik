using System.Globalization;
using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal static class DecisionTableEvaluator
{
    public const string InvoiceSchema = "invoice-approval.v1";
    private static readonly IReadOnlyDictionary<string, string> InvoiceFields = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["invoice.id"] = "Id",
        ["invoice.total"] = "Decimal",
        ["invoice.currency"] = "Currency",
        ["invoice.status"] = "String",
        ["invoice.customerId"] = "Id",
        ["invoice.isDuplicate"] = "Boolean",
        ["invoice.hasRiskConcern"] = "Boolean",
    };
    private static readonly string[] RequiredInvoiceFields = ["invoice.id", "invoice.total", "invoice.currency", "invoice.status"];
    private static readonly HashSet<string> Operators = ["eq", "ne", "gt", "gte", "lt", "lte", "in", "isMissing"];

    public static IReadOnlyList<DecisionFactField> FactFields() => InvoiceFields
        .Select(field => new DecisionFactField(field.Key, field.Value, RequiredInvoiceFields.Contains(field.Key))).ToList();

    public static IReadOnlyList<string> ComparisonOperators() => Operators.Order(StringComparer.Ordinal).ToList();

    public static IReadOnlyList<WorkflowDiagnostic> Validate(DecisionTable table, string path = "decision")
    {
        var errors = new List<WorkflowDiagnostic>();
        if (table.SchemaVersion != 1 || table.FactSchema != InvoiceSchema || table.HitPolicy != "FirstMatch")
            errors.Add(new("DecisionSchema", path, "Use schema 1, invoice-approval.v1 and FirstMatch."));
        if (!WorkflowDefinitionCodec.IsKey(table.DefaultRoute))
            errors.Add(new("DefaultRouteRequired", path, "A default route is required."));
        if (table.Fixtures?.Count > 50)
            errors.Add(new("FixtureLimit", path, "Provide at most 50 fixtures."));
        if (table.Rows is null || table.Rows.Count > 200)
        {
            errors.Add(new("DecisionRows", path, "Provide at most 200 rows."));
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var priorities = new HashSet<int>();
        foreach (DecisionRow row in table.Rows)
        {
            if (row is null || !WorkflowDefinitionCodec.IsKey(row.Id) || !ids.Add(row.Id))
            {
                errors.Add(new("DuplicateRow", path, "Row IDs must be nonempty and unique."));
                continue;
            }
            string rowPath = $"{path}.rows[{row.Id}]";
            if (!priorities.Add(row.Priority)) errors.Add(new("DuplicatePriority", rowPath, "Priorities must be unique."));
            if (!WorkflowDefinitionCodec.IsKey(row.Route)) errors.Add(new("RouteRequired", rowPath, "A valid route key is required."));
            if (row.All is null || row.All.Count == 0 || row.All.Count > 20)
            {
                errors.Add(new("ComparisonsRequired", rowPath, "Provide between 1 and 20 comparisons."));
                continue;
            }

            foreach (DecisionComparison comparison in row.All)
            {
                if (comparison is null || !InvoiceFields.TryGetValue(comparison.Field ?? "", out string? type)
                    || !Operators.Contains(comparison.Operator ?? ""))
                {
                    errors.Add(new("UnknownComparison", rowPath, "Use a declared invoice field and supported operator."));
                    continue;
                }
                if (comparison.Operator == "isMissing") continue;
                if (comparison.Value is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    errors.Add(new("ComparisonValue", rowPath, "A typed comparison value is required."));
                    continue;
                }

                bool valid = comparison.Operator == "in"
                    ? value.ValueKind == JsonValueKind.Array && value.GetArrayLength() is > 0 and <= 100
                        && value.EnumerateArray().All(item => IsValidValue(type, item))
                    : IsValidValue(type, value);
                if (!valid) errors.Add(new("ComparisonType", rowPath, "Comparison values must match the declared fact type; decimal amounts use invariant strings."));
                if (comparison.Operator is "gt" or "gte" or "lt" or "lte" && type != "Decimal")
                    errors.Add(new("ComparisonOperator", rowPath, "Ordered comparisons require a decimal fact."));
            }
        }

        var conditions = new HashSet<string>(StringComparer.Ordinal);
        foreach (DecisionRow row in table.Rows.Where(row => row?.All is not null).OrderBy(row => row.Priority))
        {
            if (!conditions.Add(JsonSerializer.Serialize(row.All)))
                errors.Add(new("ShadowedRow", $"{path}.rows[{row.Id}]", "An earlier row has identical comparisons.", "Warning"));
        }
        return errors;
    }

    public static DecisionEvaluation Evaluate(DecisionTable table, IReadOnlyDictionary<string, JsonElement> facts)
    {
        IReadOnlyList<WorkflowDiagnostic> errors = Validate(table);
        if (errors.Any(error => error.Severity == "Error")) return new("Blocked", [], [], [], errors.First(error => error.Severity == "Error").Message);
        foreach (string field in RequiredInvoiceFields)
        {
            if (!facts.TryGetValue(field, out JsonElement value) || IsMissing(value))
                return new("NeedsManualReview", [], [], [], $"Required fact {field} is missing.");
        }
        foreach ((string field, JsonElement value) in facts)
        {
            if (!InvoiceFields.TryGetValue(field, out string? type) || (!IsMissing(value) && !IsValidValue(type, value)))
                return new("NeedsManualReview", [], [], [], $"Fact {field} is not valid for {InvoiceSchema}.");
        }

        var trace = new List<DecisionRowTrace>();
        foreach (DecisionRow row in table.Rows.OrderBy(row => row.Priority))
        {
            var comparisons = new List<DecisionComparisonTrace>();
            foreach (DecisionComparison comparison in row.All)
            {
                bool present = facts.TryGetValue(comparison.Field, out JsonElement actual) && !IsMissing(actual);
                bool matches = comparison.Operator == "isMissing" ? !present : present && Matches(comparison, actual);
                comparisons.Add(new(comparison.Field, comparison.Operator, matches,
                    present ? actual.Clone() : null, comparison.Value?.Clone()));
            }
            bool matched = comparisons.All(comparison => comparison.Matched);
            trace.Add(new(row.Id, matched, comparisons));
            if (matched) return new("Completed", [row.Route], [row.Id], trace, "FirstMatchingRow");
        }

        return new("Completed", [table.DefaultRoute], [], trace, "DefaultRoute");
    }

    private static bool Matches(DecisionComparison comparison, JsonElement actual)
    {
        JsonElement expected = comparison.Value!.Value;
        string type = InvoiceFields[comparison.Field];
        if (comparison.Operator == "in") return expected.EnumerateArray().Any(item => Compare(type, actual, item) == 0);
        int order = Compare(type, actual, expected);
        return comparison.Operator switch
        {
            "eq" => order == 0,
            "ne" => order != 0,
            "gt" => order > 0,
            "gte" => order >= 0,
            "lt" => order < 0,
            "lte" => order <= 0,
            _ => false,
        };
    }

    private static int Compare(string type, JsonElement actual, JsonElement expected) => type switch
    {
        "Decimal" => ParseDecimal(actual).CompareTo(ParseDecimal(expected)),
        "Boolean" => actual.GetBoolean().CompareTo(expected.GetBoolean()),
        "Id" => Guid.Parse(actual.GetString()!).CompareTo(Guid.Parse(expected.GetString()!)),
        _ => string.Compare(actual.GetString(), expected.GetString(), StringComparison.Ordinal),
    };

    private static bool IsValidValue(string type, JsonElement value) => type switch
    {
        "Decimal" => value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
        "Boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "Id" => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out Guid id) && id != Guid.Empty,
        "Currency" => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: 3 } currency
            && currency.All(character => character is >= 'A' and <= 'Z'),
        _ => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 128 },
    };

    private static decimal ParseDecimal(JsonElement value) => decimal.Parse(value.GetString()!,
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static bool IsMissing(JsonElement value) => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
}
