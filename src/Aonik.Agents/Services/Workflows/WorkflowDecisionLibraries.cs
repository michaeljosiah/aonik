using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;

namespace Aonik.Agents.Services.Workflows;

/// <summary>One pure table, published through the existing workflow lifecycle. No separate rule graph interpreter.</summary>
internal static class WorkflowDecisionLibraries
{
    internal static bool IsReference(WorkflowDefinitionNode node) => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision)
        && node.Parameters.ValueKind == JsonValueKind.Object && node.Parameters.EnumerateObject().Any(property => string.Equals(property.Name, "library", StringComparison.OrdinalIgnoreCase));

    internal static DecisionLibraryReference Reference(WorkflowDefinitionNode node)
    {
        DecisionLibraryReference value = node.Parameters.Deserialize<DecisionLibraryReference>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Provide an exact decision-library reference.");
        WorkflowControlContracts.Pin(value.Library);
        if (value.SchemaVersion != 1 || value.Inputs is null || value.Inputs.Count > 7 || value.Routes is null || value.Routes.Count is < 1 or > 51
            || value.Routes.Any(route => !WorkflowDefinitionCodec.IsKey(route)) || value.Routes.Distinct(StringComparer.Ordinal).Count() != value.Routes.Count
            || value.OutputType is not ("Route" or "InvoiceList")) throw new ArgumentException("Declare bounded library inputs, exact output routes and Route or InvoiceList output type.");
        foreach ((string name, WorkflowInputBinding binding) in value.Inputs)
        {
            DecisionFactField field = DecisionTableEvaluator.FactFields().SingleOrDefault(field => field.Name == name)
                ?? throw new ArgumentException("A library input is not a declared invoice fact.");
            if (binding is null || binding.Step is not null || binding.Output is not null || (binding.Fact is null) == (binding.Literal is null)
                || (binding.Fact is { } fact && !DecisionTableEvaluator.FactFields().Any(source => source.Name == fact && source.Type == field.Type))
                || (binding.Literal is { } literal && !DecisionTableEvaluator.ValidFactValue(field.Type, literal)))
                throw new ArgumentException("Library mappings use one typed literal or declared compatible fact; they cannot change execution authority.");
        }
        return value;
    }

    internal static (DecisionLibraryContract Contract, DecisionTable Table) Read(WorkflowDefinition definition)
    {
        if (definition.SchemaVersion != 2 || definition.DefinitionKind != "DecisionLibrary"
            || definition.Nodes.Any(node => !WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger)
                && !WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision) && !WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.End))
            || definition.Nodes.Count(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger)) != 1
            || definition.Nodes.Count(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision)) != 1
            || definition.Nodes.Any(IsReference)) throw new ArgumentException("A library is one pure Trigger/Decision/End table in schema 2; actions, agents, connectors and nested references are unavailable.");
        DecisionLibraryContract contract = definition.Nodes.Single(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger))
            .Parameters.Deserialize<DecisionLibraryTriggerParameters>(WorkflowDefinitionCodec.JsonOptions)?.LibraryContract ?? throw new ArgumentException("Declare the library's typed inputs and outputs on its trigger.");
        DecisionTable table = definition.Nodes.Single(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision))
            .Parameters.Deserialize<DecisionTable>(WorkflowDefinitionCodec.JsonOptions) ?? throw new ArgumentException("A pure decision table is required.");
        if (contract.SchemaVersion != 1 || table.SchemaVersion != 2 || DecisionTableEvaluator.Validate(table).Any(error => error.Severity == "Error")
            || contract.Inputs is null || contract.Inputs.Count is < 1 or > 7
            || contract.Inputs.Any(field => field is null || !DecisionTableEvaluator.FactFields().Any(allowed => allowed.Name == field.Name && allowed.Type == field.Type))
            || contract.Inputs.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != contract.Inputs.Count
            || contract.Routes is null || !contract.Routes.Order(StringComparer.Ordinal).SequenceEqual(DecisionTableEvaluator.Routes(table).Order(StringComparer.Ordinal))
            || contract.OutputType != (table.HitPolicy == "Collect" ? "InvoiceList" : "Route"))
            throw new ArgumentException("Library input types and output routes/list type must match its validated schema 2 table exactly.");
        HashSet<string> declared = contract.Inputs.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        foreach (DecisionRow row in table.Rows)
        {
            IEnumerable<string> used = row.Condition is null ? row.All.Select(comparison => comparison.Field) : Fields(row.Condition);
            if (used.Any(field => !declared.Contains(field)) || row.CollectInvoiceId?.Fact is { } collected && !declared.Contains(collected))
                throw new ArgumentException("Every table fact must be declared by its library contract.");
        }
        if (table.Currency is not null && !declared.Contains("invoice.currency")) throw new ArgumentException("The selected currency must be a declared library input.");
        foreach (WorkflowDefinitionNode end in definition.Nodes.Where(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.End)))
            if (end.Parameters.EnumerateObject().Any(property => property.Name != "outcome" || property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() != "Completed"))
                throw new ArgumentException("Library End nodes return the declared table output; they cannot override its outcome.");
        return (contract, table);
    }

    internal static void ValidateReference(DecisionLibraryReference reference, WorkflowDefinition library)
    {
        var (contract, _) = Read(library);
        if (!reference.Routes.Order(StringComparer.Ordinal).SequenceEqual(contract.Routes.Order(StringComparer.Ordinal)) || reference.OutputType != contract.OutputType
            || reference.Inputs.Keys.Any(name => !contract.Inputs.Any(field => field.Name == name))
            || contract.Inputs.Any(field => field.Required && !reference.Inputs.ContainsKey(field.Name)))
            throw new ArgumentException("The consumer must map required declared inputs and preserve the pinned library's exact output contract.");
    }

    internal static DecisionEvaluation Evaluate(DecisionLibraryReference reference, WorkflowDefinition library,
        IReadOnlyDictionary<string, JsonElement> facts)
    {
        ValidateReference(reference, library); var (contract, table) = Read(library);
        var mapped = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach ((string name, WorkflowInputBinding binding) in reference.Inputs)
        {
            if (binding.Literal is { } literal) mapped.Add(name, literal.Clone());
            else if (facts.TryGetValue(binding.Fact!, out JsonElement value)) mapped.Add(name, value.Clone());
        }
        DecisionEvaluation result = DecisionTableEvaluator.EvaluateAdvanced(table, mapped, contract.Inputs);
        return result with { Library = new(reference.Library.VersionId, reference.Library.DefinitionHash, mapped) };
    }

    internal static DecisionEvaluation EvaluateDefinition(WorkflowDefinition library, IReadOnlyDictionary<string, JsonElement> facts)
    {
        var (contract, table) = Read(library);
        return facts.Keys.Any(name => !contract.Inputs.Any(field => field.Name == name))
            ? new("NeedsManualReview", [], [], [], "UndeclaredLibraryInput") : DecisionTableEvaluator.EvaluateAdvanced(table, facts, contract.Inputs);
    }

    internal static IEnumerable<string> Fields(DecisionPredicate predicate) => predicate.Comparison is { } comparison ? [comparison.Field]
        : (predicate.Children ?? []).SelectMany(Fields);
}
