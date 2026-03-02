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
            Outcome = string.IsNullOrWhiteSpace(outcome) ? "Completed" : outcome.Trim()
        };

        _dbContext.AiRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return run.Id;
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
