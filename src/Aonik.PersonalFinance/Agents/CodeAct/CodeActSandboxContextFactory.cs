using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// Builds a fresh <see cref="CodeActSandboxContext"/> from the current
/// request scope. Centralised here so all three sub-agent descriptors
/// resolve tenant/user/runId the same way.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CodeActSandboxContext.RunId"/> is generated fresh per call
/// rather than threaded through from the streaming endpoint, because (a)
/// no <c>IAgentRunContext</c> exists in this codebase, and (b) the only
/// thing the RunId controls is the sandbox session identifier — fresh per
/// build means each sub-agent invocation gets its own warm Python interp.
/// </para>
/// </remarks>
internal static class CodeActSandboxContextFactory
{
    public static CodeActSandboxContext Resolve(IServiceProvider sp, string subAgentName)
        => Resolve(sp, subAgentName, snapshot: null);

    /// <summary>
    /// Builds a fresh sandbox context, preferring the explicit
    /// <paramref name="snapshot"/> captured by the parent's tool (see
    /// <c>SubAgentImpersonationSnapshot</c> in <c>Agents/SubAgentImpersonation.cs</c>)
    /// over whatever the scoped <see cref="ITenantContext"/>/<see cref="ICurrentUserContext"/>
    /// happen to expose right now. The snapshot path is the only correct one
    /// for a sub-agent invoked from an impersonated playground run — without
    /// it, the nonce bakes in whichever user the scope happens to read at
    /// this moment, and the ACA Sessions callback then faithfully re-applies
    /// that value, which is only correct by coincidence.
    /// </summary>
    public static CodeActSandboxContext Resolve(
        IServiceProvider sp,
        string subAgentName,
        SubAgentImpersonationSnapshot? snapshot)
    {
        // Tolerant by design: missing tenant → Guid.Empty. The sub-agent's host
        // tools (callbacks for AcaSessions, direct for Hyperlight + tool-loop)
        // will surface their own errors when they actually try to query
        // tenant-scoped data, with messages that pinpoint the missing scope.
        // Throwing here makes the entire sub-agent path crash with the opaque
        // MAF wrapper "Error: Function failed." which is unactionable.
        var tenantId = snapshot?.TenantId
            ?? sp.GetRequiredService<ITenantContext>().TenantId
            ?? Guid.Empty;

        var userId = snapshot?.UserId
            ?? sp.GetRequiredService<ICurrentUserContext>().UserId;

        var runId = Guid.NewGuid().ToString("N");
        return new CodeActSandboxContext(
            SubAgentName: subAgentName,
            RunId: runId,
            TenantId: tenantId,
            CurrentUserId: userId);
    }
}
