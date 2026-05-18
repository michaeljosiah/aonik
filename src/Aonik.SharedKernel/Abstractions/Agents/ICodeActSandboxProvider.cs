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
