using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Agents.Approval;

/// <summary>
/// Default <see cref="IToolApprovalGate"/>. Aggregates every registered
/// <see cref="IToolApprovalManifest"/> and wraps each classified mutating tool in an
/// <see cref="ApprovalGatedAIFunction"/>. Fails closed: an unclassified tool whose name looks
/// like a mutation throws <see cref="ToolNotClassifiedException"/> at gate time (Spec 032 C3).
/// </summary>
public sealed class ToolApprovalGate : IToolApprovalGate
{
    private readonly IReadOnlyList<IToolApprovalManifest> _manifests;
    private readonly IToolApprovalAuditSink _auditSink;

    public ToolApprovalGate(
        IEnumerable<IToolApprovalManifest> manifests,
        IToolApprovalAuditSink auditSink)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        _manifests = manifests.ToArray();
        _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
    }

    public IEnumerable<AITool> GateAll(IEnumerable<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return tools.Select(Gate);
    }

    public AITool Gate(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var classification = Classify(tool.Name);

        if (classification is null)
        {
            // No module claims this tool. Fail closed for anything that looks like a mutation;
            // let read-looking tools through so reads need no ceremony.
            if (MutatingToolNameHeuristic.LooksMutating(tool.Name))
            {
                throw new ToolNotClassifiedException(tool.Name);
            }

            return tool;
        }

        if (classification.IsReadOnly)
        {
            return tool;
        }

        // A mutating classification must wrap an invokable AIFunction. Frontend / declaration-only
        // tools cannot be gated this way, so refuse to ship one classified as a mutation.
        if (tool is not AIFunction function)
        {
            throw new InvalidOperationException(
                $"Agent tool '{tool.Name}' is classified as a mutation but is not an AIFunction, " +
                "so the approval gate cannot wrap it.");
        }

        return new ApprovalGatedAIFunction(function, classification.Options!, _auditSink);
    }

    private ToolClassification? Classify(string toolName)
    {
        foreach (var manifest in _manifests)
        {
            var classification = manifest.Classify(toolName);
            if (classification is not null)
            {
                return classification;
            }
        }

        return null;
    }
}
