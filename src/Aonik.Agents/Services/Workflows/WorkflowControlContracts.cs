using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowControlContracts
{
    public const string ChildActionKey = "workflow.child";
    public const string LoopActionKey = "workflow.foreach";
    public const string ParallelActionKey = "workflow.parallel";
    public static WorkflowActionDescriptor ChildDescriptor { get; } = new(ChildActionKey, 1, "workflow_child", "Workflow.Run", null,
        JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{"invoiceId":{"type":"string","format":"uuid"}},"required":["invoiceId"],"additionalProperties":false}"""),
        JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{"childRunId":{"type":"string","format":"uuid"},"outcome":{"type":"string"}},"required":["childRunId","outcome"]}"""));

    public static bool IsComposition(WorkflowDefinitionNode node) => node.Kind.Equals(WorkflowNodeKinds.Child, StringComparison.OrdinalIgnoreCase)
        || node.Kind.Equals(WorkflowNodeKinds.Loop, StringComparison.OrdinalIgnoreCase) || node.Kind.Equals(WorkflowNodeKinds.Parallel, StringComparison.OrdinalIgnoreCase);

    public static WorkflowChildParameters Child(WorkflowDefinitionNode node)
    {
        WorkflowChildParameters value = node.Parameters.Deserialize<WorkflowChildParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Provide a typed child workflow contract.");
        Pin(value.Workflow); Binding(value.InvoiceId); Routes(value.CompletedRoute, value.ErrorRoute);
        if (value.SchemaVersion != 1) throw new ArgumentException("Use child contract schema 1.");
        return value;
    }

    public static WorkflowLoopParameters Loop(WorkflowDefinitionNode node)
    {
        WorkflowLoopParameters value = node.Parameters.Deserialize<WorkflowLoopParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Provide a bounded foreach contract.");
        Pin(value.Body); Routes(value.CompletedRoute, value.ExhaustedRoute, value.ErrorRoute);
        if (value.SchemaVersion is not (1 or 2) || (value.SchemaVersion == 2) != (value.ItemsFromDecision is not null)
            || (value.ItemsFromDecision is not null && (!WorkflowDefinitionCodec.IsKey(value.ItemsFromDecision) || value.Items?.Count != 0))
            || value.MaxIterations is < 1 or > WorkflowControlLimits.MaximumLoopIterations
            || value.TimeoutSeconds is < 1 or > WorkflowControlLimits.MaximumLifetimeSeconds || value.Items is null || value.Items.Count > 100
            || value.Items.Any(item => item is null || !IsItemKey(item.Key) || item.InvoiceId == Guid.Empty)
            || value.Items.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != value.Items.Count)
            throw new ArgumentException("Use up to 100 uniquely keyed Invoice items, a 1–100 iteration limit and a deadline of at most seven days.");
        return value;
    }

    public static WorkflowParallelParameters Parallel(WorkflowDefinitionNode node)
    {
        WorkflowParallelParameters value = node.Parameters.Deserialize<WorkflowParallelParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Provide bounded named branches.");
        Routes(value.CompletedRoute, value.ErrorRoute);
        if (value.SchemaVersion != 1 || value.Branches is null || value.Branches.Count is < 2 or > WorkflowControlLimits.MaximumBranches
            || value.MaxConcurrency < 1 || value.MaxConcurrency > value.Branches.Count || value.JoinPolicy is not ("All" or "AnySuccess")
            || value.Branches.Any(branch => branch is null || !IsItemKey(branch.Key))
            || value.Branches.Select(branch => branch.Key).Distinct(StringComparer.Ordinal).Count() != value.Branches.Count)
            throw new ArgumentException("Use 2–4 uniquely named branches, bounded concurrency and All or AnySuccess.");
        foreach (WorkflowParallelBranch branch in value.Branches) { Pin(branch.Workflow); Binding(branch.InvoiceId); }
        return value;
    }

    public static IReadOnlyList<string> Routes(WorkflowDefinitionNode node) => node.Kind.ToLowerInvariant() switch
    {
        "child" => [Child(node).CompletedRoute, Child(node).ErrorRoute],
        "loop" => [Loop(node).CompletedRoute, Loop(node).ExhaustedRoute, Loop(node).ErrorRoute],
        "parallel" => [Parallel(node).CompletedRoute, Parallel(node).ErrorRoute],
        _ => throw new ArgumentException("Unsupported workflow control."),
    };

    public static void Binding(WorkflowInputBinding? binding)
    {
        if (binding is null || (binding.Literal.HasValue ? 1 : 0) + (binding.Fact is not null ? 1 : 0)
                + (binding.Step is not null || binding.Output is not null ? 1 : 0) != 1
            || (binding.Step is null) != (binding.Output is null)
            || (binding.Fact is not null && binding.Fact != "invoice.id")
            || (binding.Literal is { } literal && (literal.ValueKind != JsonValueKind.String || !literal.TryGetGuid(out Guid id) || id == Guid.Empty)))
            throw new ArgumentException("Map the child's Invoice identifier from one UUID literal, invoice.id fact, or declared prior UUID output.");
    }

    internal static void Pin(WorkflowVersionPin? pin)
    {
        if (pin is null || pin.VersionId == Guid.Empty || pin.DefinitionHash is not { Length: 64 }
            || !pin.DefinitionHash.All(Uri.IsHexDigit)) throw new ArgumentException("Pin an exact published workflow version and SHA-256 definition hash.");
    }
    private static bool IsItemKey(string? key) => key is { Length: <= 32 } && WorkflowDefinitionCodec.IsKey(key);
    private static void Routes(params string[] routes)
    {
        if (routes.Any(route => !WorkflowDefinitionCodec.IsKey(route) || route.Length > 100) || routes.Distinct(StringComparer.Ordinal).Count() != routes.Length)
            throw new ArgumentException("Configure distinct stable completed, error and exhaustion routes.");
    }
}
