using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class AiRunWriter : IAiRunWriter
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AiRunWriter(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
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

        var model = await EnsureDefaultModelAsync(cancellationToken);

        var run = new AiRun
        {
            TenantId = _tenantProvider.GetCurrentTenantId(),
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

    private async Task UpdateRunOutcomeAsync(
        Guid aiRunId,
        string outcome,
        string? outputRef,
        CancellationToken cancellationToken)
    {
        var run = await _dbContext.AiRuns
            .FirstOrDefaultAsync(item => item.Id == aiRunId, cancellationToken)
            ?? throw new InvalidOperationException($"AiRun {aiRunId} not found.");

        run.Outcome = outcome;
        run.OutputRef = string.IsNullOrWhiteSpace(outputRef) ? null : outputRef.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
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
