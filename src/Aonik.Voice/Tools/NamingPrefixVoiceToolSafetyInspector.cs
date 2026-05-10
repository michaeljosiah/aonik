using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Voice.Tools;

/// <summary>
/// Naming-prefix classifier — the v1 simple option per Phase 1.5 of the spec.
/// AONIK doesn't have a server-side approval/policy wrapper for backend tools,
/// so v1 enforces "agents propose, systems execute" by filtering mutating tools
/// out of the agent's tool list before any voice connection.
///
/// <para>
/// Configurable: callers can supply additional read-only/mutating prefixes via
/// the constructor for tools that don't follow the <c>pf_*</c> convention.
/// </para>
/// </summary>
internal sealed class NamingPrefixVoiceToolSafetyInspector : IVoiceToolSafetyInspector
{
    // Defaults match the audit target list in spec Phase 1.5.
    private static readonly string[] DefaultReadOnlyPrefixes =
    [
        "pf_get_",
        "pf_list_",
        "pf_search_",
        "pf_describe_",
        "pf_summarize_",
    ];

    private static readonly string[] DefaultMutatingPrefixes =
    [
        "pf_create_",
        "pf_update_",
        "pf_archive_",
        "pf_delete_",
        "pf_apply_",
        "pf_cancel_",
        "pf_confirm_",
        "pf_reject_",
        "pf_set_",
        "pf_override_",
    ];

    private readonly IReadOnlyList<string> _readOnlyPrefixes;
    private readonly IReadOnlyList<string> _mutatingPrefixes;
    private readonly ILogger<NamingPrefixVoiceToolSafetyInspector> _logger;

    /// <summary>Default constructor — uses the spec's pf_* prefix lists.</summary>
    public NamingPrefixVoiceToolSafetyInspector(
        ILogger<NamingPrefixVoiceToolSafetyInspector>? logger = null)
        : this(DefaultReadOnlyPrefixes, DefaultMutatingPrefixes, logger)
    { }

    /// <summary>
    /// Test/extension constructor — allows callers (or future tenant configuration)
    /// to override the prefix lists without changing code.
    /// </summary>
    public NamingPrefixVoiceToolSafetyInspector(
        IReadOnlyList<string> readOnlyPrefixes,
        IReadOnlyList<string> mutatingPrefixes,
        ILogger<NamingPrefixVoiceToolSafetyInspector>? logger = null)
    {
        _readOnlyPrefixes = readOnlyPrefixes ?? throw new ArgumentNullException(nameof(readOnlyPrefixes));
        _mutatingPrefixes = mutatingPrefixes ?? throw new ArgumentNullException(nameof(mutatingPrefixes));
        _logger = logger ?? NullLogger<NamingPrefixVoiceToolSafetyInspector>.Instance;
    }

    public VoiceToolClassification Classify(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return VoiceToolClassification.Unknown;

        foreach (var prefix in _readOnlyPrefixes)
        {
            if (toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return VoiceToolClassification.ReadOnly;
        }

        foreach (var prefix in _mutatingPrefixes)
        {
            if (toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return VoiceToolClassification.Mutating;
        }

        return VoiceToolClassification.Unknown;
    }

    public IReadOnlyList<AITool> FilterReadOnly(IReadOnlyList<AITool> tools)
    {
        if (tools is null || tools.Count == 0)
            return Array.Empty<AITool>();

        var allowed = new List<AITool>(tools.Count);
        foreach (var tool in tools)
        {
            var classification = Classify(tool.Name);
            switch (classification)
            {
                case VoiceToolClassification.ReadOnly:
                    allowed.Add(tool);
                    break;
                case VoiceToolClassification.Mutating:
                    _logger.LogDebug(
                        "Voice: filtering mutating tool {ToolName} from voice agent variant",
                        tool.Name);
                    break;
                case VoiceToolClassification.Unknown:
                    _logger.LogWarning(
                        "Voice: tool {ToolName} did not match any known read-only or mutating prefix; treating as mutating and excluding from voice. Update IVoiceToolSafetyInspector prefix lists.",
                        tool.Name);
                    break;
            }
        }

        return allowed;
    }

    public IReadOnlySet<string> FilterReadOnlyNames(IReadOnlyList<string> toolNames)
    {
        if (toolNames is null || toolNames.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allowed = new HashSet<string>(toolNames.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var name in toolNames)
        {
            switch (Classify(name))
            {
                case VoiceToolClassification.ReadOnly:
                    allowed.Add(name);
                    break;
                case VoiceToolClassification.Mutating:
                    _logger.LogDebug(
                        "Voice: filtering mutating tool {ToolName} from voice agent variant",
                        name);
                    break;
                case VoiceToolClassification.Unknown:
                    _logger.LogWarning(
                        "Voice: tool {ToolName} did not match any known read-only or mutating prefix; treating as mutating and excluding from voice. Update IVoiceToolSafetyInspector prefix lists.",
                        name);
                    break;
            }
        }

        return allowed;
    }
}
