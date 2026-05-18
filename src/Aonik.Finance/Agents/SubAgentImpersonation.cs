using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Agents;

/// <summary>
/// Optional contract implemented by Spec 025 sub-agent descriptors
/// (<c>pf-insights</c>, <c>pf-forecast</c>, <c>pf-classify</c>) so the
/// parent's tool can hand them a <see cref="SubAgentImpersonationSnapshot"/>
/// captured before any awaits. Descriptors that implement this MUST:
/// <list type="bullet">
///   <item>Forward <paramref>snapshot</paramref> to <see cref="CodeAct.CodeActSandboxContextFactory"/>
///   so the nonce baked into the sandbox carries the snapshot's UserId
///   (not whatever the scope happens to expose).</item>
///   <item>Wrap each host <see cref="AIFunction"/> with
///   <see cref="ContextRestoringAIFunction"/> in the tool-loop fallback path
///   so the inner read-only services see the snapshot on every invocation.</item>
/// </list>
/// </summary>
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
/// Tenant + user snapshot captured by a parent agent's tool before it builds
/// a sub-agent. Spec 025 sub-agents (pf-insights / pf-forecast / pf-classify)
/// resolve <see cref="ICurrentUserContext"/> and <see cref="ITenantContext"/>
/// from the request scope, so anything that mutates those between the parent
/// reading impersonation state and the sub-agent's tool actually executing
/// (other middleware, fresh DI scopes from coordinators like
/// <c>VoiceSynthCoordinator</c>, or any continuation that runs without
/// <c>HttpContext</c>) silently flips the sub-agent onto the wrong user's data.
/// </summary>
/// <param name="UserId">
/// Impersonated end-user ID the parent saw at the moment it decided to invoke
/// the sub-agent. Null only when no impersonation override is active and the
/// caller is themselves the end user.
/// </param>
/// <param name="TenantId">
/// Tenant ID the parent ran under. Captured alongside UserId because both must
/// be re-applied atomically before any tenant-filtered query runs.
/// </param>
internal sealed record SubAgentImpersonationSnapshot(Guid? UserId, Guid? TenantId)
{
    public static SubAgentImpersonationSnapshot Empty { get; } = new(null, null);

    public bool HasOverride => UserId.HasValue || TenantId.HasValue;
}

/// <summary>
/// <see cref="DelegatingAIFunction"/> that re-applies a captured
/// <see cref="SubAgentImpersonationSnapshot"/> onto the request scope's
/// <see cref="ICurrentUserContext"/> + <see cref="ITenantContext"/> immediately
/// before delegating to the wrapped tool. Wrap each sub-agent host tool with
/// this so the inner read-only services see the impersonated user the parent
/// captured — never whatever the scope happens to expose at invocation time.
/// </summary>
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

        // Diagnostic — surfaces only when the wrapper actually had to repair
        // scope drift, which is the failure mode we're guarding against.
        // Stays as Information so it lights up in dev logs / App Insights
        // without flooding production with no-op entries.
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
