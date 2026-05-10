using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Voice.Tools;

/// <summary>
/// v1 implementation: queries the descriptor for its tool name list, filters
/// to the read-only subset via <see cref="IVoiceToolSafetyInspector"/>, then
/// rebuilds the agent via the descriptor's
/// <c>Build(IChatClient, IServiceProvider, instructionsOverride, allowedToolNames)</c>
/// overload — which already supports filtering. The original agent
/// (built by <c>IDomainAgentResolver</c> for AGUI) is untouched.
/// </summary>
internal sealed class VoiceAgentBuilder : IVoiceAgentBuilder
{
    private readonly IVoiceToolSafetyInspector _inspector;
    private readonly ILogger<VoiceAgentBuilder> _logger;

    public VoiceAgentBuilder(
        IVoiceToolSafetyInspector inspector,
        ILogger<VoiceAgentBuilder>? logger = null)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _logger = logger ?? NullLogger<VoiceAgentBuilder>.Instance;
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
