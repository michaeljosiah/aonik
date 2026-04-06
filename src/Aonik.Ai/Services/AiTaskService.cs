using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class AiTaskService : IAiTaskService
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public AiTaskService(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<AiTaskResponse>> ListAsync(string? category = null, CancellationToken ct = default)
    {
        var query = _dbContext.AiTasks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        var tasks = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(ct);

        // Resolve primary model names via route policies
        var useCases = tasks.Select(t => t.UseCase).Distinct().ToList();

        var routePolicies = await _dbContext.AiRoutePolicies
            .Where(rp => useCases.Contains(rp.UseCase))
            .ToListAsync(ct);

        var modelIds = routePolicies
            .Select(rp => rp.PrimaryModelId)
            .Distinct()
            .ToList();

        var models = await _dbContext.AiModels
            .Where(m => modelIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.ModelName, ct);

        var policyByUseCase = routePolicies
            .GroupBy(rp => rp.UseCase)
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(rp => rp.TenantId != null) ?? g.First());

        return tasks.Select(t => MapToResponse(t, policyByUseCase, models)).ToList();
    }

    public async Task<AiTaskDetailResponse?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _dbContext.AiTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return null;

        // Resolve route policy
        var routePolicy = await _dbContext.AiRoutePolicies
            .Where(rp => rp.UseCase == task.UseCase)
            .OrderByDescending(rp => rp.TenantId)
            .FirstOrDefaultAsync(ct);

        string? primaryModelName = null;
        if (routePolicy is not null)
        {
            var model = await _dbContext.AiModels
                .FirstOrDefaultAsync(m => m.Id == routePolicy.PrimaryModelId, ct);
            primaryModelName = model?.ModelName;
        }

        // Aggregate stats from AiRuns
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);

        var runs = await _dbContext.AiRuns
            .Where(r => r.UseCase == task.UseCase)
            .ToListAsync(ct);

        var totalRuns = runs.Count;
        var last24hRuns = runs.Count(r => r.CreatedAt >= last24h);
        var avgLatencyMs = totalRuns > 0 ? runs.Average(r => r.LatencyMs) : 0;
        var avgCost = totalRuns > 0 ? runs.Average(r => r.CostEstimate) : 0m;
        var successRate = totalRuns > 0
            ? (double)runs.Count(r => r.Outcome == "Completed") / totalRuns
            : 0;
        var lastRunAt = runs
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (DateTime?)r.CreatedAt)
            .FirstOrDefault();

        var stats = new AiTaskStatsResponse
        {
            TotalRuns = totalRuns,
            Last24hRuns = last24hRuns,
            AvgLatencyMs = avgLatencyMs,
            AvgCost = avgCost,
            SuccessRate = successRate,
            LastRunAt = lastRunAt,
        };

        return new AiTaskDetailResponse
        {
            Id = task.Id,
            TenantId = task.TenantId,
            UseCase = task.UseCase,
            DisplayName = task.DisplayName,
            Description = task.Description,
            Category = task.Category,
            ExecutionMode = task.ExecutionMode,
            PromptName = task.PromptName,
            PromptVersion = task.PromptVersion,
            SystemTemplate = task.SystemTemplate,
            UserTemplate = task.UserTemplate,
            DeveloperTemplate = task.DeveloperTemplate,
            VariablesSchemaJson = task.VariablesSchemaJson,
            OutputSchemaJson = task.OutputSchemaJson,
            IsPublished = task.IsPublished,
            IsActive = task.IsActive,
            PrimaryModelName = primaryModelName,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Stats = stats,
            RoutePolicyId = routePolicy?.Id,
            RoutePolicyRiskTier = routePolicy?.RiskTier,
            RoutePolicyDataSensitivity = routePolicy?.DataSensitivity,
        };
    }

    public async Task<AiTaskResponse> CreateAsync(CreateAiTaskRequest request, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantScope();

        var task = new AiTask
        {
            TenantId = tenantId,
            UseCase = request.UseCase,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Category = request.Category,
            ExecutionMode = request.ExecutionMode,
            PromptName = request.PromptName,
            PromptVersion = request.PromptVersion,
            SystemTemplate = request.SystemTemplate,
            UserTemplate = request.UserTemplate ?? string.Empty,
            DeveloperTemplate = request.DeveloperTemplate ?? string.Empty,
            VariablesSchemaJson = request.VariablesSchemaJson ?? string.Empty,
            OutputSchemaJson = request.OutputSchemaJson ?? string.Empty,
            IsPublished = request.IsPublished,
            IsActive = request.IsActive,
        };

        _dbContext.AiTasks.Add(task);
        await _dbContext.SaveChangesAsync(ct);

        return MapToResponse(task);
    }

    public async Task<AiTaskResponse> UpdateAsync(Guid id, UpdateAiTaskRequest request, CancellationToken ct = default)
    {
        var task = await _dbContext.AiTasks.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException($"AiTask with ID {id} not found.");

        ApplyUpdates(task, request);
        await _dbContext.SaveChangesAsync(ct);

        return MapToResponse(task);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _dbContext.AiTasks.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException($"AiTask with ID {id} not found.");

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    private Guid? ResolveTenantScope()
        => _tenantProvider.TryGetCurrentTenantId(out var tenantId) ? tenantId : null;

    private static void ApplyUpdates(AiTask task, UpdateAiTaskRequest request)
    {
        if (request.DisplayName is not null) task.DisplayName = request.DisplayName;
        if (request.Description is not null) task.Description = request.Description;
        if (request.Category is not null) task.Category = request.Category;
        if (request.ExecutionMode is not null) task.ExecutionMode = request.ExecutionMode;
        if (request.PromptName is not null) task.PromptName = request.PromptName;
        if (request.PromptVersion is not null) task.PromptVersion = request.PromptVersion;
        if (request.SystemTemplate is not null) task.SystemTemplate = request.SystemTemplate;
        if (request.UserTemplate is not null) task.UserTemplate = request.UserTemplate;
        if (request.DeveloperTemplate is not null) task.DeveloperTemplate = request.DeveloperTemplate;
        if (request.VariablesSchemaJson is not null) task.VariablesSchemaJson = request.VariablesSchemaJson;
        if (request.OutputSchemaJson is not null) task.OutputSchemaJson = request.OutputSchemaJson;
        if (request.IsPublished.HasValue) task.IsPublished = request.IsPublished.Value;
        if (request.IsActive.HasValue) task.IsActive = request.IsActive.Value;
    }

    private static AiTaskResponse MapToResponse(
        AiTask task,
        Dictionary<string, AiRoutePolicy>? policyByUseCase = null,
        Dictionary<Guid, string>? models = null)
    {
        string? primaryModelName = null;
        if (policyByUseCase is not null
            && policyByUseCase.TryGetValue(task.UseCase, out var policy)
            && models is not null
            && models.TryGetValue(policy.PrimaryModelId, out var modelName))
        {
            primaryModelName = modelName;
        }

        return new AiTaskResponse
        {
            Id = task.Id,
            TenantId = task.TenantId,
            UseCase = task.UseCase,
            DisplayName = task.DisplayName,
            Description = task.Description,
            Category = task.Category,
            ExecutionMode = task.ExecutionMode,
            PromptName = task.PromptName,
            PromptVersion = task.PromptVersion,
            SystemTemplate = task.SystemTemplate,
            UserTemplate = task.UserTemplate,
            DeveloperTemplate = task.DeveloperTemplate,
            VariablesSchemaJson = task.VariablesSchemaJson,
            OutputSchemaJson = task.OutputSchemaJson,
            IsPublished = task.IsPublished,
            IsActive = task.IsActive,
            PrimaryModelName = primaryModelName,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        };
    }
}
