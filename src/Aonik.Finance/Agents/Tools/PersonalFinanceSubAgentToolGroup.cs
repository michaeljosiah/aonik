using System.Text.Json;
using Aonik.Finance.Agents;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Shared base for the two personal-finance ("Simi") tool groups that delegate
/// to Spec 025 analytical sub-agents — <see cref="PersonalFinanceInsightTools"/>
/// (pf-insights / pf-forecast / pf-classify) and
/// <see cref="PersonalFinanceCompassTools"/> (pf-compass-planner). Holds only
/// the machinery those two groups have in common: capturing the parent's
/// impersonation snapshot, resolving a descriptor, building the structured
/// sub-agent, logging sub-agent failures, and formatting an exception for a
/// synthesised error response. The CRUD groups don't run sub-agents, so they
/// take no base at all and carry none of these dependencies (the cohesion goal
/// of #118 / Spec 027 S1).
/// </summary>
internal abstract class PersonalFinanceSubAgentToolGroup
{
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentConfigurationService _agentConfigurationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    protected PersonalFinanceSubAgentToolGroup(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        IAgentConfigurationService agentConfigurationService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _agentConfigurationService = agentConfigurationService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    /// <summary>The current-user provider, exposed for snapshot-history reads that
    /// resolve the authenticated user id (e.g. pf_list_snapshot_history / pf_compare_snapshots).</summary>
    protected ICurrentUserProvider CurrentUserProvider => _currentUserProvider;

    // ── Sub-agent error handling ──────────────────────────────────
    //
    // Failures inside a sub-agent run (Microsoft.Agents.AI / Microsoft.Extensions.AI
    // exceptions, EF query errors thrown by a tool, structured-output schema
    // validation, etc.) used to bubble up to the parent agent as the generic
    // "Function failed" string with no detail — unactionable in the playground
    // and bad for the customer experience. We now catch them, log the full
    // exception, and synthesise a valid structured response that carries the
    // exception type + message in the warnings / reason codes. The parent agent
    // can read that and surface a helpful message; the original error still
    // shows up in logs for the developer.

    protected void LogSubAgentException(string subAgentName, string userQuestion, Exception ex)
    {
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger("PersonalFinanceTools.SubAgent");
        logger.LogError(
            ex,
            "Sub-agent {SubAgent} failed for question '{Question}': {Message}",
            subAgentName,
            userQuestion,
            ex.Message);
    }

    protected static string FormatExceptionForResponse(Exception ex)
    {
        // Keep the message short enough that Simi can paraphrase it without
        // hitting context-window pressure, but include the type + inner-
        // exception chain so the playground reveals enough to act on.
        var lines = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
        var inner = ex.InnerException;
        var depth = 0;
        while (inner is not null && depth < 3)
        {
            lines.Add($"  caused by {inner.GetType().Name}: {inner.Message}");
            inner = inner.InnerException;
            depth++;
        }
        var joined = string.Join('\n', lines);
        return joined.Length > 1200 ? joined[..1200] + "..." : joined;
    }

    protected IDomainAgentDescriptor ResolveSubAgentDescriptor(string name)
    {
        var descriptor = _serviceProvider
            .GetServices<IDomainAgentDescriptor>()
            .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));

        return descriptor
            ?? throw new InvalidOperationException(
                $"The '{name}' sub-agent descriptor is not registered in DI. Check FinanceModule.ConfigureServices.");
    }

    /// <summary>
    /// Captures the parent's current user + tenant synchronously, before any
    /// awaits, so a Spec 025 sub-agent invoked via <c>RunInsights</c> /
    /// <c>RunForecast</c> / <c>RunClassifyReview</c> sees exactly the
    /// impersonated identity the parent saw at the moment it decided to
    /// delegate — see SubAgentImpersonation.cs for the full rationale. Either
    /// value can be null (e.g. background fixtures with no tenant/user
    /// resolved); the sub-agent falls back to the scoped resolution in that
    /// case, which is the ordinary non-impersonated behaviour and unchanged
    /// by this fix.
    /// </summary>
    protected SubAgentImpersonationSnapshot CaptureImpersonationSnapshot()
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        var tenantId = _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId)
            ? (Guid?)resolvedTenantId
            : null;
        return new SubAgentImpersonationSnapshot(userId, tenantId);
    }

    protected async Task<ChatClientAgent> BuildStructuredSubAgentAsync(
        IDomainAgentDescriptor descriptor,
        SubAgentImpersonationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var config = await _agentConfigurationService.GetResolvedAsync(descriptor.Name, cancellationToken);

        string? instructionsOverride = null;
        HashSet<string>? allowedToolNames = null;

        if (config is not null)
        {
            instructionsOverride = !string.IsNullOrWhiteSpace(config.InstructionsText)
                ? config.InstructionsText
                : null;

            if (!string.IsNullOrWhiteSpace(config.ToolsetIdsJson) && config.ToolsetIdsJson != "[]")
            {
                try
                {
                    var toolNames = JsonSerializer.Deserialize<List<string>>(config.ToolsetIdsJson);
                    if (toolNames is { Count: > 0 })
                    {
                        allowedToolNames = new HashSet<string>(toolNames, StringComparer.Ordinal);
                    }
                }
                catch (JsonException)
                {
                    allowedToolNames = null;
                }
            }
        }

        // Spec 025 sub-agents (pf-insights / pf-forecast / pf-classify) implement
        // ISubAgentDescriptor so the captured snapshot flows into
        // CodeActSandboxContextFactory (ACA Sessions nonce) and wraps every host
        // tool with ContextRestoringAIFunction (tool-loop fallback). Any other
        // IDomainAgentDescriptor (e.g. pf-compass-planner, which takes its
        // financial context as a request payload and never resolves the scoped
        // user/tenant itself) keeps its original, unmodified Build path.
        var builtAgent = descriptor switch
        {
            ISubAgentDescriptor subAgent => subAgent.BuildWithImpersonation(
                _chatClient,
                _serviceProvider,
                instructionsOverride,
                allowedToolNames,
                snapshot),
            _ when config is null => descriptor.Build(_chatClient, _serviceProvider),
            _ => descriptor.Build(_chatClient, _serviceProvider, instructionsOverride, allowedToolNames),
        };

        return builtAgent as ChatClientAgent
            ?? throw new InvalidOperationException($"The agent '{descriptor.Name}' must be a ChatClientAgent.");
    }
}
