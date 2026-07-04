using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Pluggable provider for the CodeAct sub-agent path (Spec 025). Each
/// implementation backs the wrapping <c>execute_code</c> tool with a different
/// sandbox technology (Hyperlight for local Linux dev, Azure Container Apps
/// Dynamic Sessions for cloud deploys, or a null no-op that triggers the
/// conventional tool-loop fallback).
/// </summary>
/// <remarks>
/// <para>
/// Sub-agent descriptors call <see cref="TryBuildExecuteCodeTool"/> once per
/// <c>Build</c> invocation. A <c>null</c> return means "this provider can't
/// service the request, fall through to the conventional tool-loop path."
/// </para>
/// <para>
/// Implementations MUST be safe to register as singletons. The returned
/// <see cref="AIFunction"/> can capture <paramref name="hostTools"/> by
/// reference but MUST NOT close over any per-request scope — those tools
/// already bind their request-scoped dependencies internally.
/// </para>
/// </remarks>
public interface ICodeActSandboxProvider
{
    /// <summary>
    /// Builds the per-invocation <c>execute_code</c> <see cref="AIFunction"/>
    /// the sub-agent surfaces to the LLM, or <c>null</c> when this provider
    /// can't run on the current host (caller falls back to the conventional
    /// tool-loop path).
    /// </summary>
    AIFunction? TryBuildExecuteCodeTool(
        CodeActSandboxContext context,
        IReadOnlyList<AIFunction> hostTools);
}

/// <summary>
/// Immutable bundle of values a <see cref="ICodeActSandboxProvider"/> needs to
/// mint a sandbox + nonce for a single sub-agent invocation. Deliberately
/// excludes <c>IServiceProvider</c> so the returned tool delegate can't close
/// over a per-request scope (captive-scope risk).
/// </summary>
/// <param name="SubAgentName">
/// Must be one of the registered Spec 025 sub-agent names
/// (<c>"pf-insights"</c>, <c>"pf-forecast"</c>, <c>"pf-classify"</c>).
/// Used by the callback endpoint to route tool resolution to the correct
/// <c>PersonalFinanceTools.CreateForXxxSubAgent</c> slice.
/// </param>
/// <param name="RunId">
/// Unguessable run identifier. Used as part of the sandbox session identifier
/// so concurrent runs in the same pool don't collide and so warm Python state
/// is reused within one run.
/// </param>
/// <param name="TenantId">
/// Tenant the run executes for. Echoed into the nonce so the callback
/// endpoint can re-establish tenant scope before dispatching the tool.
/// </param>
/// <param name="CurrentUserId">
/// Impersonated user the run executes for (the playground User Brief picker
/// sets this). Null when the run is purely admin-scoped.
/// </param>
public sealed record CodeActSandboxContext(
    string SubAgentName,
    string RunId,
    Guid TenantId,
    Guid? CurrentUserId);

/// <summary>
/// User + tenant snapshot captured by a parent agent's tool (e.g.
/// <c>PersonalFinanceTools.RunInsights</c>) immediately before it resolves and
/// builds a Spec 025 sub-agent descriptor — synchronously, before any awaits.
/// </summary>
/// <remarks>
/// <para>
/// Lives on SharedKernel (alongside <see cref="CodeActSandboxContext"/>)
/// rather than in <c>Aonik.Finance.Agents</c> because
/// <c>CodeActSandboxContextFactory</c> — which needs to accept this snapshot
/// to prefer it over the ambient scope when baking an ACA Sessions nonce —
/// now lives in the <c>Aonik.PersonalFinance</c> assembly (Spec 027 Phase 5,
/// in progress). <c>Aonik.PersonalFinance</c> deliberately has no
/// <c>ProjectReference</c> back to <c>Aonik.Finance</c> (siblings, not
/// parent/child — see ADR-006), so a type this factory accepts as a parameter
/// cannot itself live in <c>Aonik.Finance</c>. Both modules already reference
/// SharedKernel, so this is the one place both sides of that call can see it.
/// </para>
/// <para>
/// Both <c>ICurrentUserContext</c> and <c>ITenantContext</c> are ordinary
/// DI-scoped services with settable properties (not immutable, not
/// AsyncLocal-backed) — nothing stops another consumer sharing the same scope
/// from mutating them between the parent's read and the sub-agent's tool
/// actually executing. Passing this snapshot through explicitly, rather than
/// trusting a second resolve of the same scoped services later, is what makes
/// the sub-agent's view of "current user" immune to that class of drift.
/// </para>
/// </remarks>
/// <param name="UserId">
/// The impersonated end-user id the parent saw at the moment it decided to
/// invoke the sub-agent. Null when no impersonation override is active and
/// the caller is themselves the end user (the ordinary Payabo/production
/// case) — in that case the sub-agent should behave exactly as before this
/// fix, relying on whatever the scope resolves.
/// </param>
/// <param name="TenantId">
/// The tenant id the parent ran under. Captured alongside <c>UserId</c>
/// because both must be re-applied together before any tenant-filtered query
/// runs — re-applying one without the other could scope a query to the right
/// user in the wrong tenant.
/// </param>
public sealed record SubAgentImpersonationSnapshot(Guid? UserId, Guid? TenantId)
{
    public static SubAgentImpersonationSnapshot Empty { get; } = new(UserId: null, TenantId: null);

    /// <summary>
    /// True when this snapshot carries an explicit override worth re-applying.
    /// Both <see cref="CodeActSandboxContext"/> callers (via
    /// <c>CodeActSandboxContextFactory</c>) and <c>ContextRestoringAIFunction</c>
    /// use this to no-op cheaply on the ordinary (non-impersonated) path
    /// instead of touching the scoped contexts on every call.
    /// </summary>
    public bool HasOverride => UserId.HasValue || TenantId.HasValue;
}
