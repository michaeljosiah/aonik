using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Agents;

/// <summary>
/// Optional contract implemented by the Spec 025 sub-agent descriptors
/// (<c>pf-insights</c>, <c>pf-forecast</c>, <c>pf-classify</c>) so
/// <see cref="Tools.PersonalFinanceTools.RunInsights"/> / <c>RunForecast</c> /
/// <c>RunClassifyReview</c> can hand them a
/// <see cref="SharedKernel.Abstractions.Agents.SubAgentImpersonationSnapshot"/>
/// captured before the sub-agent's own async build work runs. Descriptors that
/// implement this MUST:
/// <list type="bullet">
///   <item>Forward the snapshot to <see cref="CodeAct.CodeActSandboxContextFactory"/>
///   so the ACA Sessions nonce bakes in the snapshot's <c>UserId</c>/<c>TenantId</c>
///   — not whatever <see cref="ICurrentUserContext"/>/<see cref="ITenantContext"/>
///   happen to resolve to at the moment <c>Build</c> runs.</item>
///   <item>Wrap each host <see cref="AIFunction"/> with
///   <see cref="ContextRestoringAIFunction"/> in the tool-loop fallback path (no
///   CodeAct provider configured) so every tool invocation re-applies the
///   snapshot immediately before running, not just once at build time.</item>
/// </list>
/// </summary>
/// <remarks>
/// Scoped to the three Spec 025 sub-agents rather than added to
/// <see cref="IDomainAgentDescriptor"/> itself — that interface has 11
/// implementations across Commerce, Finance, Platform, and PersonalFinance, and
/// only these three ever get invoked mid-request from a parent agent's own tool
/// call (the "sub-agent-of-a-sub-agent" shape unique to Spec 025). Everything
/// else builds directly from a fresh request scope and has no snapshot to
/// preserve.
/// </remarks>
internal interface ISubAgentDescriptor : IDomainAgentDescriptor
{
    AIAgent BuildWithImpersonation(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames,
        SubAgentImpersonationSnapshot snapshot);
}

/// <summary>
/// <see cref="DelegatingAIFunction"/> that re-applies a captured
/// <see cref="SubAgentImpersonationSnapshot"/> onto the request-scoped
/// <see cref="ICurrentUserContext"/> + <see cref="ITenantContext"/> immediately
/// before delegating to the wrapped tool. Used to wrap each sub-agent host tool
/// in the tool-loop fallback path (no CodeAct provider available) so the
/// inner read-only services see the parent's impersonated user on every
/// invocation — not just whichever value the scope happened to hold when the
/// sub-agent was built.
/// </summary>
/// <remarks>
/// Same shape as <see cref="Aonik.SharedKernel.Agents.Approval.ApprovalGatedAIFunction"/>
/// (Spec 032): a sealed decorator over <see cref="DelegatingAIFunction"/> that
/// captures an <see cref="IServiceProvider"/> at wrap time and resolves
/// request-scoped services lazily inside <see cref="InvokeCoreAsync"/>, so it
/// always reads the live scoped instance rather than a stale reference.
/// </remarks>
internal sealed class ContextRestoringAIFunction : DelegatingAIFunction
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SubAgentImpersonationSnapshot _snapshot;

    public ContextRestoringAIFunction(
        AIFunction inner,
        IServiceProvider serviceProvider,
        SubAgentImpersonationSnapshot snapshot)
        : base(inner)
    {
        _serviceProvider = serviceProvider;
        _snapshot = snapshot;
    }

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        RestoreScopedContext();
        return base.InvokeCoreAsync(arguments, cancellationToken);
    }

    private void RestoreScopedContext()
    {
        if (!_snapshot.HasOverride)
        {
            // Ordinary (non-impersonated) path: nothing to restore, and
            // touching the scoped contexts here would be needless overhead
            // on every single tool call for the common case.
            return;
        }

        var userContext = _serviceProvider.GetService<ICurrentUserContext>();
        var tenantContext = _serviceProvider.GetService<ITenantContext>();
        var observedUserId = userContext?.UserId;
        var observedTenantId = tenantContext?.TenantId;
        var changedUser = false;
        var changedTenant = false;

        if (_snapshot.UserId is { } userId && userContext is not null && userContext.UserId != userId)
        {
            userContext.UserId = userId;
            changedUser = true;
        }

        if (_snapshot.TenantId is { } tenantId && tenantContext is not null && tenantContext.TenantId != tenantId)
        {
            tenantContext.TenantId = tenantId;
            tenantContext.ResolutionSource = "sub-agent-impersonation";
            changedTenant = true;
        }

        // Diagnostic only — fires exclusively when the wrapper actually had to
        // repair scope drift, which is precisely the failure mode this
        // wrapper exists to guard against. Information level so it lights up
        // in dev logs / App Insights without adding noise on every call.
        if (changedUser || changedTenant)
        {
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("PersonalFinanceTools.SubAgentImpersonation");
            logger?.LogInformation(
                "Sub-agent tool {ToolName}: restored impersonation (observed user={ObservedUserId}, snapshot user={SnapshotUserId}, observed tenant={ObservedTenantId}, snapshot tenant={SnapshotTenantId})",
                Name,
                observedUserId,
                _snapshot.UserId,
                observedTenantId,
                _snapshot.TenantId);
        }
    }
}
