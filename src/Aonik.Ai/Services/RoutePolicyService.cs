using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class RoutePolicyService : IRoutePolicyService
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public RoutePolicyService(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<RoutePolicyResponse>> ListAsync(string? useCase = null, CancellationToken ct = default)
    {
        var query = _dbContext.AiRoutePolicies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(useCase))
            query = query.Where(p => p.UseCase.Contains(useCase));

        var policies = await query
            .OrderBy(p => p.UseCase)
            .ThenByDescending(p => p.TenantId.HasValue)
            .ToListAsync(ct);

        // Resolve model names for display
        var modelIds = policies
            .Where(p => p.PrimaryModelId != Guid.Empty)
            .Select(p => p.PrimaryModelId)
            .Distinct()
            .ToList();

        var modelNames = modelIds.Count > 0
            ? await _dbContext.AiModels
                .Where(m => modelIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.ModelName, ct)
            : new Dictionary<Guid, string>();

        return policies.Select(p => MapToResponse(p, modelNames)).ToList();
    }

    public async Task<RoutePolicyResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await _dbContext.AiRoutePolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null) return null;

        string? modelName = null;
        if (policy.PrimaryModelId != Guid.Empty)
        {
            modelName = await _dbContext.AiModels
                .Where(m => m.Id == policy.PrimaryModelId)
                .Select(m => m.ModelName)
                .FirstOrDefaultAsync(ct);
        }

        return MapToResponse(policy, modelName);
    }

    public async Task<RoutePolicyResponse> CreateAsync(CreateRoutePolicyRequest request, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantScope();

        var existing = await _dbContext.AiRoutePolicies
            .FirstOrDefaultAsync(
                p => p.UseCase == request.UseCase
                    && p.TenantId == tenantId,
                ct);

        if (existing is not null)
            throw new InvalidOperationException(
                $"An AI route policy for use-case '{request.UseCase}' already exists in this scope.");

        var policy = new AiRoutePolicy
        {
            TenantId = tenantId,
            UseCase = request.UseCase,
            RiskTier = request.RiskTier,
            DataSensitivity = request.DataSensitivity,
            CostCeiling = request.CostCeiling,
            PrimaryModelId = request.PrimaryModelId,
            FallbackModelIdsJson = request.FallbackModelIdsJson ?? "[]",
            IsActive = request.IsActive,
        };

        _dbContext.AiRoutePolicies.Add(policy);
        await _dbContext.SaveChangesAsync(ct);

        string? modelName = null;
        if (policy.PrimaryModelId != Guid.Empty)
        {
            modelName = await _dbContext.AiModels
                .Where(m => m.Id == policy.PrimaryModelId)
                .Select(m => m.ModelName)
                .FirstOrDefaultAsync(ct);
        }

        return MapToResponse(policy, modelName);
    }

    public async Task<RoutePolicyResponse> UpdateAsync(Guid id, UpdateRoutePolicyRequest request, CancellationToken ct = default)
    {
        var policy = await _dbContext.AiRoutePolicies.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"AiRoutePolicy with ID {id} not found.");

        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId) && policy.TenantId is null)
        {
            var tenantOverride = await _dbContext.AiRoutePolicies.FirstOrDefaultAsync(
                p => p.UseCase == policy.UseCase
                    && p.TenantId == tenantId,
                ct);

            if (tenantOverride is null)
            {
                tenantOverride = CloneForTenant(policy, tenantId);
                _dbContext.AiRoutePolicies.Add(tenantOverride);
            }

            ApplyUpdates(tenantOverride, request);
            await _dbContext.SaveChangesAsync(ct);

            var tenantOverrideModelName = await ResolveModelNameAsync(tenantOverride.PrimaryModelId, ct);
            return MapToResponse(tenantOverride, tenantOverrideModelName);
        }

        ApplyUpdates(policy, request);

        await _dbContext.SaveChangesAsync(ct);

        var modelName = await ResolveModelNameAsync(policy.PrimaryModelId, ct);

        return MapToResponse(policy, modelName);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await _dbContext.AiRoutePolicies.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"AiRoutePolicy with ID {id} not found.");

        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            if (policy.TenantId is null)
                throw new InvalidOperationException("Global route policies cannot be deleted from a tenant context.");

            if (policy.TenantId != tenantId)
                throw new InvalidOperationException("Tenant mismatch detected for the requested route policy.");
        }

        policy.IsDeleted = true;
        policy.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    private Guid? ResolveTenantScope()
        => _tenantProvider.TryGetCurrentTenantId(out var tenantId) ? tenantId : null;

    private static void ApplyUpdates(AiRoutePolicy policy, UpdateRoutePolicyRequest request)
    {
        if (request.RiskTier is not null) policy.RiskTier = request.RiskTier;
        if (request.DataSensitivity is not null) policy.DataSensitivity = request.DataSensitivity;
        if (request.CostCeiling.HasValue) policy.CostCeiling = request.CostCeiling.Value;
        if (request.PrimaryModelId.HasValue) policy.PrimaryModelId = request.PrimaryModelId.Value;
        if (request.FallbackModelIdsJson is not null) policy.FallbackModelIdsJson = request.FallbackModelIdsJson;
        if (request.IsActive.HasValue) policy.IsActive = request.IsActive.Value;
    }

    private static AiRoutePolicy CloneForTenant(AiRoutePolicy source, Guid tenantId) => new()
    {
        TenantId = tenantId,
        UseCase = source.UseCase,
        RiskTier = source.RiskTier,
        DataSensitivity = source.DataSensitivity,
        CostCeiling = source.CostCeiling,
        PrimaryModelId = source.PrimaryModelId,
        FallbackModelIdsJson = source.FallbackModelIdsJson,
        IsActive = source.IsActive,
    };

    private async Task<string?> ResolveModelNameAsync(Guid modelId, CancellationToken ct)
    {
        if (modelId == Guid.Empty)
            return null;

        return await _dbContext.AiModels
            .Where(m => m.Id == modelId)
            .Select(m => m.ModelName)
            .FirstOrDefaultAsync(ct);
    }

    private static RoutePolicyResponse MapToResponse(AiRoutePolicy policy, Dictionary<Guid, string> modelNames)
    {
        modelNames.TryGetValue(policy.PrimaryModelId, out var modelName);
        return MapToResponse(policy, modelName);
    }

    private static RoutePolicyResponse MapToResponse(AiRoutePolicy policy, string? modelName) => new()
    {
        Id = policy.Id,
        TenantId = policy.TenantId,
        UseCase = policy.UseCase,
        RiskTier = policy.RiskTier,
        DataSensitivity = policy.DataSensitivity,
        CostCeiling = policy.CostCeiling,
        PrimaryModelId = policy.PrimaryModelId,
        PrimaryModelName = modelName,
        FallbackModelIdsJson = policy.FallbackModelIdsJson,
        IsActive = policy.IsActive,
        CreatedAt = policy.CreatedAt,
        UpdatedAt = policy.UpdatedAt,
    };
}
