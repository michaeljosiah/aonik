using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Voice.Tools;

/// <summary>
/// Builds the voice agent variant. The Spec 032 default (<see cref="BuildVariant"/>) exposes the
/// full gated toolset when the descriptor's mutating tools are classified by the approval manifest —
/// each mutating tool is wrapped by <see cref="IToolApprovalGate"/> so Medium/High calls are enforced
/// server-side and surface an approval card over voice. When the agent has unclassified mutating
/// tools (the gate would throw, fail-closed) it falls back to <see cref="BuildReadOnlyVariant"/>,
/// which filters to the read-only subset via <see cref="IVoiceToolSafetyInspector"/>. Either way it
/// rebuilds through the descriptor's
/// <c>Build(IChatClient, IServiceProvider, instructionsOverride, allowedToolNames)</c> overload; the
/// original agent (built by <c>IDomainAgentResolver</c> for AGUI) is untouched.
/// </summary>
internal sealed class VoiceAgentBuilder : IVoiceAgentBuilder
{
    private readonly IVoiceToolSafetyInspector _inspector;
    private readonly IToolApprovalGate _approvalGate;
    private readonly ILogger<VoiceAgentBuilder> _logger;

    public VoiceAgentBuilder(
        IVoiceToolSafetyInspector inspector,
        IToolApprovalGate approvalGate,
        ILogger<VoiceAgentBuilder>? logger = null)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _approvalGate = approvalGate ?? throw new ArgumentNullException(nameof(approvalGate));
        _logger = logger ?? NullLogger<VoiceAgentBuilder>.Instance;
    }

    public VoiceAgentBuildResult BuildVariant(
        IDomainAgentDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // descriptor.GetToolNames already runs the names through the gate, so these are the gated
        // tool names. Classify each to decide whether the full gated toolset is safe to expose:
        // it is only when at least one tool is a CLASSIFIED mutation AND none is an unclassified
        // mutating-looking tool (which would make the descriptor's gated Build throw, fail-closed).
        var allToolNames = descriptor.GetToolNames(serviceProvider);

        var hasClassifiedMutating = false;
        var hasUnclassifiedMutating = false;
        foreach (var name in allToolNames)
        {
            var classification = _approvalGate.Classify(name);
            if (classification is { IsMutating: true })
            {
                hasClassifiedMutating = true;
            }
            else if (classification is null && MutatingToolNameHeuristic.LooksMutating(name))
            {
                hasUnclassifiedMutating = true;
            }
        }

        if (hasClassifiedMutating && !hasUnclassifiedMutating)
        {
            var chatClient = serviceProvider.GetRequiredService<IChatClient>();

            // Build the FULL agent the same way AG-UI does — the descriptor's Build applies
            // gate.GateAll, wrapping every classified mutating tool so it cannot run ungated. The
            // approval result flows back through the agent loop and the voice pipeline emits a
            // confirmAction toolCall envelope (Spec 032 §7.7).
            var gatedAgent = descriptor.Build(chatClient, serviceProvider);

            _logger.LogInformation(
                "Voice: built FULL gated variant of agent {AgentName} — {ToolCount} tool(s) exposed; " +
                "mutating tools are enforced server-side by the approval gate.",
                descriptor.Name,
                allToolNames.Count);

            return new VoiceAgentBuildResult(
                gatedAgent,
                allToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                VoiceAgentToolMode.Gated);
        }

        if (hasUnclassifiedMutating)
        {
            _logger.LogInformation(
                "Voice: agent {AgentName} has unclassified mutating-looking tool(s); falling back to the " +
                "read-only variant (the approval gate would fail closed on the full toolset).",
                descriptor.Name);
        }

        return BuildReadOnlyVariant(descriptor, serviceProvider);
    }

    public VoiceAgentBuildResult BuildReadOnlyVariant(
        IDomainAgentDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var chatClient = serviceProvider.GetRequiredService<IChatClient>();

        var allToolNames = descriptor.GetToolNames(serviceProvider);
        var allowedToolNames = _inspector.FilterReadOnlyNames(allToolNames);

        var removed = new List<string>(Math.Max(0, allToolNames.Count - allowedToolNames.Count));
        foreach (var name in allToolNames)
        {
            if (!allowedToolNames.Contains(name))
                removed.Add(name);
        }

        if (removed.Count > 0)
        {
            _logger.LogInformation(
                "Voice: built read-only variant of agent {AgentName} — {AllowedCount} tool(s) allowed, {RemovedCount} removed: {RemovedNames}",
                descriptor.Name,
                allowedToolNames.Count,
                removed.Count,
                string.Join(", ", removed));
        }
        else
        {
            _logger.LogDebug(
                "Voice: built read-only variant of agent {AgentName} — all {ToolCount} tool(s) classified as read-only",
                descriptor.Name,
                allowedToolNames.Count);
        }

        // Use the descriptor's existing override to rebuild with the filtered
        // tool set. Instructions stay as the agent's defaults — voice etiquette
        // adjustments come from the descriptor's own prompt (per spec, no
        // separate "voice prompt overlay" in v1).
        var voiceAgent = descriptor.Build(
            chatClient,
            serviceProvider,
            instructionsOverride: null,
            allowedToolNames: allowedToolNames);

        return new VoiceAgentBuildResult(voiceAgent, allowedToolNames, removed);
    }
}
