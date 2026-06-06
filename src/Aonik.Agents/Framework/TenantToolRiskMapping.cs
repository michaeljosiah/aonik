using Aonik.Agents.Entities;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Framework;

/// <summary>
/// Maps a persisted <see cref="TenantToolRiskTier"/> onto the Spec 032 <see cref="ToolClassification"/>
/// the gate consumes (Spec 033 §8.5). Mutating tenant tools default to High; the mapping never
/// downgrades a mutating-looking tool to read-only on its own — only an explicit
/// <see cref="TenantToolRiskTier.ReadOnly"/> (set by a PlatformAdmin) does that.
/// </summary>
internal static class TenantToolRiskMapping
{
    public static ToolApprovalTier ToApprovalTier(TenantToolRiskTier tier) => tier switch
    {
        TenantToolRiskTier.Low => ToolApprovalTier.Low,
        TenantToolRiskTier.Medium => ToolApprovalTier.Medium,
        // High, and any unknown value, map to High — fail safe.
        _ => ToolApprovalTier.High,
    };

    /// <summary>Classify a declarative HTTP tool from its explicit per-row tier.</summary>
    public static ToolClassification ClassifyHttpTool(TenantHttpTool tool)
    {
        if (tool.RiskTier == TenantToolRiskTier.ReadOnly)
        {
            return ToolClassification.ReadOnly;
        }

        return ToolClassification.Mutating(new ToolApprovalOptions(
            ToApprovalTier(tool.RiskTier),
            ActionKind: string.IsNullOrWhiteSpace(tool.ActionKind) ? tool.Name : tool.ActionKind,
            ProposalType: string.IsNullOrWhiteSpace(tool.ProposalType)
                ? $"Tenant.Http.{tool.Name}"
                : tool.ProposalType));
    }

    /// <summary>
    /// Classify a discovered MCP tool. If the server's default tier is ReadOnly the PlatformAdmin has
    /// vouched the whole server is safe, so every tool passes through. Otherwise a read-looking name
    /// is read-only and a mutating-looking name inherits the server's (mutating) default tier — so a
    /// server defaulted to High exposes its reads directly and gates its writes as High.
    /// </summary>
    public static ToolClassification ClassifyMcpTool(string toolName, TenantMcpServer server)
    {
        if (server.DefaultRiskTier == TenantToolRiskTier.ReadOnly
            || !MutatingToolNameHeuristic.LooksMutating(toolName))
        {
            return ToolClassification.ReadOnly;
        }

        return ToolClassification.Mutating(new ToolApprovalOptions(
            ToApprovalTier(server.DefaultRiskTier),
            ActionKind: $"{server.Name}: {toolName}",
            ProposalType: $"Tenant.Mcp.{server.Name}.{toolName}"));
    }
}
