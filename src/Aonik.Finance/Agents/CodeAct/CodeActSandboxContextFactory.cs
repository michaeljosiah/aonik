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
    {
        var tenantId = sp.GetRequiredService<ITenantContext>().TenantId
            ?? throw new InvalidOperationException(
                "Cannot build CodeAct sandbox context: tenant scope is not resolved.");
        var userContext = sp.GetRequiredService<ICurrentUserContext>();
        var runId = Guid.NewGuid().ToString("N");
        return new CodeActSandboxContext(
            SubAgentName: subAgentName,
            RunId: runId,
            TenantId: tenantId,
            CurrentUserId: userContext.UserId);
    }
}
