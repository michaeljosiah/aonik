using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Workflows.Graph;

internal static class WorkflowInputBinder
{
    public static JsonElement Bind(WorkflowToolParameters parameters, WorkflowFactsSnapshot facts,
        IReadOnlyDictionary<string, WorkflowActionResult> outputs)
    {
        if (parameters.Inputs is null || parameters.Inputs.Count > 50) throw new ArgumentException("Provide at most 50 action input bindings.");
        var arguments = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach ((string name, WorkflowInputBinding binding) in parameters.Inputs)
        {
            int sources = (binding.Literal.HasValue ? 1 : 0) + (binding.Fact is not null ? 1 : 0)
                + (binding.Step is not null || binding.Output is not null ? 1 : 0);
            if (sources != 1) throw new ArgumentException("Each action input must select exactly one literal, fact or prior output.");
            JsonElement value;
            if (binding.Literal is { } literal) value = literal;
            else if (binding.Fact is { } fact)
            {
                if (!facts.Facts.TryGetValue(fact, out value)) throw new ArgumentException($"Required fact {fact} is unavailable.");
            }
            else if (binding.Step is { } step && binding.Output is { } output
                && outputs.TryGetValue(step, out WorkflowActionResult? result) && result.Output.ValueKind == JsonValueKind.Object
                && result.Output.TryGetProperty(output, out value)) { }
            else throw new ArgumentException("The referenced prior action output is unavailable.");
            arguments.Add(name, value.Clone());
        }
        JsonElement bound = JsonSerializer.SerializeToElement(arguments);
        if (bound.GetRawText().Length > 65_536) throw new ArgumentException("Action arguments exceed the supported size.");
        return bound;
    }

    public static void ValidateNames(WorkflowToolParameters parameters, WorkflowActionDescriptor descriptor)
    {
        if (parameters.Inputs is null || descriptor.InputSchema.ValueKind != JsonValueKind.Object
            || !descriptor.InputSchema.TryGetProperty("properties", out JsonElement properties))
            throw new ArgumentException("The action input schema is unavailable.");
        foreach (string name in parameters.Inputs.Keys)
            if (!properties.TryGetProperty(name, out _)) throw new ArgumentException($"Unknown action input {name}.");
        if (descriptor.InputSchema.TryGetProperty("required", out JsonElement required))
            foreach (JsonElement name in required.EnumerateArray())
                if (!parameters.Inputs.ContainsKey(name.GetString()!)) throw new ArgumentException($"Action input {name.GetString()} is required.");
    }
}
