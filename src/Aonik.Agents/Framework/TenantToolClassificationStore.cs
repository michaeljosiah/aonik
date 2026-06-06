using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Framework;

/// <summary>
/// Request-scoped registry mapping a tenant-contributed tool's name to its Spec 032
/// <see cref="ToolClassification"/>. The tenant tool providers (remote MCP — Spec 033 §8.3, and
/// declarative HTTP — §8.4) populate it as they materialise the current tenant's tools at
/// agent-build time; the singleton <see cref="TenantToolApprovalManifest"/> reads it so those
/// tools flow through the SAME fail-closed gate as the built-ins.
/// <para>
/// It is scoped — one tenant per request — so a bare tool name resolves unambiguously without
/// re-deriving the tenant. The providers register a classification BEFORE handing their tools to
/// <c>IToolApprovalGate.GateAll</c>, so by the time the gate calls the manifest the entry exists.
/// </para>
/// </summary>
public interface ITenantToolClassificationStore
{
    /// <summary>Record the classification for a tenant tool name for this request (last write wins).</summary>
    void Register(string toolName, ToolClassification classification);

    /// <summary>
    /// Return the classification for <paramref name="toolName"/>, or <see langword="null"/> if it is
    /// not a tenant tool registered this request (the gate then applies its default rules).
    /// </summary>
    ToolClassification? Find(string toolName);
}

/// <summary>
/// Default in-memory <see cref="ITenantToolClassificationStore"/>. Registered scoped, so each
/// request gets its own map and there is no cross-tenant bleed.
/// </summary>
internal sealed class TenantToolClassificationStore : ITenantToolClassificationStore
{
    private readonly Dictionary<string, ToolClassification> _map = new(StringComparer.Ordinal);

    public void Register(string toolName, ToolClassification classification)
    {
        if (string.IsNullOrEmpty(toolName) || classification is null)
        {
            return;
        }

        _map[toolName] = classification;
    }

    public ToolClassification? Find(string toolName) =>
        !string.IsNullOrEmpty(toolName) && _map.TryGetValue(toolName, out var classification)
            ? classification
            : null;
}
