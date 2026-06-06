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
    /// Classify a discovered MCP tool — fail closed. A remote tenant MCP server can advertise arbitrary
    /// tool names, so the tool name is NOT trusted to imply read-only: a side-effecting tool named
    /// <c>send_invoice</c> / <c>charge_card</c> / <c>email_customer</c> would not trip a verb heuristic
    /// yet must still be gated. The only way a tenant MCP tool runs ungated is an explicit PlatformAdmin
    /// read-only classification — today that means the whole server's <see cref="TenantMcpServer.DefaultRiskTier"/>
    /// is <see cref="TenantToolRiskTier.ReadOnly"/> (a per-tool override is the future seam, Spec 033 §9).
    /// Otherwise every discovered tool inherits the server's (mutating) tier — High by default → durable
    /// proposal — so an unknown tool can never bypass the Spec 032 gate.
    /// </summary>
    public static ToolClassification ClassifyMcpTool(string toolName, TenantMcpServer server)
    {
        // Only an explicit PlatformAdmin "this server is read-only" lets its tools execute directly.
        if (server.DefaultRiskTier == TenantToolRiskTier.ReadOnly)
        {
            return ToolClassification.ReadOnly;
        }

        // Every other discovered tool — regardless of name — inherits the server's mutating tier.
        return ToolClassification.Mutating(new ToolApprovalOptions(
            ToApprovalTier(server.DefaultRiskTier),
            ActionKind: $"{server.Name}: {toolName}",
            ProposalType: $"Tenant.Mcp.{server.Name}.{toolName}"));
    }
}
