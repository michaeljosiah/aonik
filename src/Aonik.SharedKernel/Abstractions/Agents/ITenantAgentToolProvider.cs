using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Supplies the current tenant's contributed agent tools (remote MCP — Spec 033 §8.3, and
/// declarative HTTP — §8.4) as <em>raw</em> <see cref="AITool"/> instances, for a domain agent
/// descriptor to concatenate into the single <c>IToolApprovalGate.GateAll(...)</c> call it already
/// makes. The implementation registers each tool's Spec 032 classification into the request-scoped
/// classification store the <c>TenantToolApprovalManifest</c> reads, BEFORE returning — so the
/// descriptor's existing gating pass wraps tenant tools exactly like built-ins, with no second
/// gating path (Spec 033 §8.5, §8.6).
/// <para>
/// Lives on SharedKernel so a domain module (Finance, …) can pull in tenant tools without a
/// back-reference to the Agents runtime. Returns an empty list when there is no current tenant or
/// the tenant has no active, approved tools — so an agent with no tenant tools builds exactly as
/// before (no regression for non-users).
/// </para>
/// </summary>
public interface ITenantAgentToolProvider
{
    /// <summary>
    /// Build the current tenant's active, approved MCP + HTTP tools as raw AITools and register
    /// their classifications for the gate. Synchronous to fit the descriptor's <c>Build</c> seam;
    /// remote MCP discovery is cached per (tenant, server, credential version) so only the first
    /// build for a server pays the connection cost.
    /// </summary>
    IReadOnlyList<AITool> GetTools(IServiceProvider serviceProvider);
}
