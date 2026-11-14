using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowActionRegistry
{
    private readonly IReadOnlyDictionary<(string Key, int Version), IWorkflowActionHandler> _handlers;

    public WorkflowActionRegistry(IEnumerable<IWorkflowActionHandler> handlers, IEnumerable<IToolApprovalManifest> manifests)
    {
        _handlers = handlers.ToDictionary(handler => (handler.Descriptor.Key, handler.Descriptor.Version));
        IToolApprovalManifest[] classifications = manifests.ToArray();
        foreach (WorkflowActionDescriptor descriptor in Descriptors)
        {
            ToolClassification? classification = classifications.Select(manifest => manifest.Classify(descriptor.ToolName)).FirstOrDefault(value => value is not null);
            if (descriptor.Approval is { } approval && (classification?.Options is not { } required
                || required.Tier != approval.Tier || required.ProposalType != approval.ProposalType))
                throw new InvalidOperationException($"Workflow action {descriptor.Key}@{descriptor.Version} does not match its enforcing tool classification.");
            if (descriptor.Approval is null && (classification?.IsMutating == true || MutatingToolNameHeuristic.LooksMutating(descriptor.ToolName)))
                throw new InvalidOperationException($"Workflow action {descriptor.Key}@{descriptor.Version} cannot expose a mutation as a read.");
        }
    }

    public IReadOnlyList<WorkflowActionDescriptor> Descriptors => _handlers.Values.Select(handler => handler.Descriptor)
        .OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal).ThenBy(descriptor => descriptor.Version).ToList();

    public IWorkflowActionHandler Resolve(string key, int version) => _handlers.TryGetValue((key, version), out IWorkflowActionHandler? handler)
        ? handler : throw new WorkflowConflictException($"Workflow action {key}@{version} is unavailable.");
}
