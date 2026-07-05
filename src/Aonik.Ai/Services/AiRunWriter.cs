using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Ai.Services;

internal sealed class AiRunWriter : IAiRunWriter
{
    // Kill-switch state is checked on every StartRunAsync call — that's
    // 12+ reads per voice run in the trace audit. Cache for 60s with
    // fail-safe; the admin endpoint that flips the switch invalidates
    // the entry so engagement still takes effect immediately. The cached
    // shape keeps just the fields we need for the kill-switch check.
    internal readonly record struct CachedKillSwitchState(
        bool Engaged,
        DateTime? EngagedAt,
        Guid? EngagedByUserId);

    private static readonly FusionCacheEntryOptions KillSwitchCacheOptions = new(TimeSpan.FromSeconds(60))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromMinutes(10),
    };

    private const string KillSwitchCacheKeyPrefix = "ai-kill-switch:v1:";
    internal static string KillSwitchCacheKey(Guid tenantId) => $"{KillSwitchCacheKeyPrefix}{tenantId:N}";

    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFusionCache _cache;
    private readonly Aonik.Ai.Observability.AiRunMetrics _metrics;

    public AiRunWriter(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFusionCache cache,
        Aonik.Ai.Observability.AiRunMetrics metrics)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cache = cache;
        _metrics = metrics;
    }

    public async Task<Guid> SaveRunAsync(
        string useCase,
        string inputRefsJson,
        string outcome,
        CancellationToken cancellationToken = default)
    {
        var aiRunId = await StartRunAsync(useCase, inputRefsJson, cancellationToken);

        var normalizedOutcome = string.IsNullOrWhiteSpace(outcome)
            ? "Completed"
            : outcome.Trim();

        if (string.Equals(normalizedOutcome, "Started", StringComparison.OrdinalIgnoreCase))
        {
            return aiRunId;
        }

        if (string.Equals(normalizedOutcome, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            await MarkRunCompletedAsync(aiRunId, cancellationToken: cancellationToken);
            return aiRunId;
        }

        await UpdateRunOutcomeAsync(aiRunId, normalizedOutcome, null, cancellationToken);
        return aiRunId;
    }

    public async Task<Guid> StartRunAsync(
        string useCase,
        string inputRefsJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(useCase))
        {
            throw new ArgumentException("useCase is required.", nameof(useCase));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Kill-switch enforcement (Wave 7b follow-up). Reads the tenant's
        // singleton TenantAgentSettings row; the absence of a row is
        // treated as "not engaged". When engaged, throw a domain exception
        // before any model resolution or DB write happens — callers
        // surface this as a friendly "agents paused" message. The state
        // is cached to spare a per-run DB hit; admin updates invalidate
        // the entry so the switch still takes effect immediately.
        var killSwitch = await _cache.GetOrSetAsync<CachedKillSwitchState>(
            KillSwitchCacheKey(tenantId),
            async cacheCt =>
            {
                var row = await _dbContext.TenantAgentSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId, cacheCt);
                return row is null
                    ? new CachedKillSwitchState(false, null, null)
                    : new CachedKillSwitchState(row.KillSwitchEngaged, row.KillSwitchEngagedAt, row.KillSwitchEngagedByUserId);
            },
            KillSwitchCacheOptions,
            cancellationToken);

        if (killSwitch.Engaged)
        {
            throw new KillSwitchEngagedException(
                tenantId,
                killSwitch.EngagedAt,
                killSwitch.EngagedByUserId);
        }

        var model = await EnsureDefaultModelAsync(cancellationToken);

        var run = new AiRun
        {
            TenantId = tenantId,
            UserId = _currentUserProvider.GetCurrentUserId(),
            UseCase = useCase.Trim(),
            AiModelId = model.Id,
            PromptSpecId = null,
            AiPolicyId = null,
            InputRefsJson = string.IsNullOrWhiteSpace(inputRefsJson) ? "{}" : inputRefsJson,
            OutputRef = null,
            TokensUsed = 0,
            CostEstimate = 0,
            LatencyMs = 0,
            Outcome = "Started"
        };

        _dbContext.AiRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return run.Id;
    }

    public async Task MarkRunCompletedAsync(
        Guid aiRunId,
        string? outputRef = null,
        CancellationToken cancellationToken = default)
    {
        await UpdateRunOutcomeAsync(aiRunId, "Completed", outputRef, cancellationToken);
    }

    public async Task MarkRunCompletedWithMetricsAsync(
        Guid aiRunId,
        int tokensUsed,
        int latencyMs,
        decimal costEstimate,
        string? outputRef = null,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadRunAsync(aiRunId, cancellationToken);

        run.Outcome = "Completed";
        run.TokensUsed = tokensUsed;
        run.LatencyMs = latencyMs;
        run.OutputRef = string.IsNullOrWhiteSpace(outputRef) ? null : outputRef.Trim();

        // Auto-compute cost from the model's CostProfileJson when the caller
        // passes zero (i.e. the caller didn't have access to model metadata).
        if (costEstimate != 0m)
        {
            run.CostEstimate = costEstimate;
        }
        else
        {
            var model = await _dbContext.AiModels
                .FirstOrDefaultAsync(m => m.Id == run.AiModelId, cancellationToken);

            if (model is not null)
            {
                // Split tokensUsed evenly as a rough approximation when
                // separate input/output counts are unavailable.
                run.CostEstimate = AiCostCalculator.ComputeCost(
                    tokensUsed / 2, tokensUsed - (tokensUsed / 2), model.CostProfileJson);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Per-tenant counters — token count is authoritative on this path
        // (the caller supplied it directly), so this is the highest-quality
        // input to the AI tokens-by-tenant dashboard.
        _metrics.RecordRunCompleted(run.TenantId, run.Outcome, run.UseCase, tokensUsed);
    }

    public async Task MarkRunFailedAsync(
        Guid aiRunId,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(failureReason)
            ? "Unknown error"
            : failureReason.Trim();

        if (normalizedReason.Length > 200)
        {
            normalizedReason = normalizedReason[..200];
        }

        await UpdateRunOutcomeAsync(aiRunId, "Failed", normalizedReason, cancellationToken);
    }

    // Prefer the instance StartRunAsync already added to this scope's change tracker over a
    // re-query: the terminal write follows the insert in the same request scope, so the row is
    // almost always tracked locally and the re-fetch is a pure wasted round-trip (H17). Fall
    // back to the DB only when completion runs in a fresh scope with nothing tracked.
    private async Task<AiRun> LoadRunAsync(Guid aiRunId, CancellationToken cancellationToken)
    {
        var tracked = _dbContext.AiRuns.Local.FirstOrDefault(item => item.Id == aiRunId);
        if (tracked is not null)
        {
            return tracked;
        }

        return await _dbContext.AiRuns
            .FirstOrDefaultAsync(item => item.Id == aiRunId, cancellationToken)
            ?? throw new InvalidOperationException($"AiRun {aiRunId} not found.");
    }

    private async Task UpdateRunOutcomeAsync(
        Guid aiRunId,
        string outcome,
        string? outputRef,
        CancellationToken cancellationToken)
    {
        var run = await LoadRunAsync(aiRunId, cancellationToken);

        run.Outcome = outcome;
        run.OutputRef = string.IsNullOrWhiteSpace(outputRef) ? null : outputRef.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Per-tenant agent-run counter + token-usage counter. Fires once
        // per terminal outcome (Completed / Failed / etc.) so dashboards
        // can chart per-tenant run volume and token consumption without
        // scraping the AnkAiRuns table. The "MarkRunCompletedWithMetrics"
        // path supplies an explicit token count; other paths land here
        // with run.TokensUsed = 0, which the metrics class skips.
        _metrics.RecordRunCompleted(run.TenantId, outcome, run.UseCase, run.TokensUsed);
    }

    private async Task<AiModel> EnsureDefaultModelAsync(CancellationToken cancellationToken)
    {
        var existingModel = await _dbContext.AiModels
            .FirstOrDefaultAsync(item => item.IsActive, cancellationToken);

        if (existingModel != null)
        {
            return existingModel;
        }

        var provider = new AiProvider
        {
            Name = "StubProvider",
            AuthConfigRef = null,
            CapabilitiesJson = "[]",
            IsActive = true
        };

        _dbContext.AiProviders.Add(provider);

        var model = new AiModel
        {
            AiProviderId = provider.Id,
            ModelName = "stub-chat-model",
            ContextWindow = 16000,
            CostProfileJson = "{}",
            LatencyProfileJson = "{}",
            PolicyTagsJson = "[]",
            IsActive = true
        };

        _dbContext.AiModels.Add(model);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return model;
    }
}
