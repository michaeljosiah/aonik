using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class PromptSpecService : IPromptSpecService
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public PromptSpecService(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<PromptSpecResponse>> ListAsync(string? name = null, CancellationToken ct = default)
    {
        var query = _dbContext.PromptSpecs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(p => p.Name.Contains(name));

        var specs = await query
            .OrderBy(p => p.Name)
            .ThenByDescending(p => p.Version)
            .ToListAsync(ct);

        return specs.Select(MapToResponse).ToList();
    }

    public async Task<PromptSpecResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var spec = await _dbContext.PromptSpecs.FirstOrDefaultAsync(p => p.Id == id, ct);
        return spec is null ? null : MapToResponse(spec);
    }

    public async Task<PromptSpecResponse> CreateAsync(CreatePromptSpecRequest request, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantScope();

        var existing = await _dbContext.PromptSpecs
            .FirstOrDefaultAsync(
                p => p.Name == request.Name
                    && p.Version == request.Version
                    && p.TenantId == tenantId,
                ct);

        if (existing is not null)
            throw new InvalidOperationException(
                $"A prompt template named '{request.Name}' with version '{request.Version}' already exists in this scope.");

        var spec = new PromptSpec
        {
            TenantId = tenantId,
            Name = request.Name,
            Version = request.Version,
            SystemTemplate = request.SystemTemplate,
            UserTemplate = request.UserTemplate ?? string.Empty,
            DeveloperTemplate = request.DeveloperTemplate ?? string.Empty,
            VariablesSchemaJson = request.VariablesSchemaJson ?? string.Empty,
            OutputSchemaJson = request.OutputSchemaJson ?? string.Empty,
            SafetyPolicyRef = request.SafetyPolicyRef,
            IsPublished = request.IsPublished,
        };

        _dbContext.PromptSpecs.Add(spec);
        await _dbContext.SaveChangesAsync(ct);

        return MapToResponse(spec);
    }

    public async Task<PromptSpecResponse> UpdateAsync(Guid id, UpdatePromptSpecRequest request, CancellationToken ct = default)
    {
        var spec = await _dbContext.PromptSpecs.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"PromptSpec with ID {id} not found.");

        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId) && spec.TenantId is null)
        {
            var tenantOverride = await _dbContext.PromptSpecs.FirstOrDefaultAsync(
                p => p.Name == spec.Name
                    && p.Version == spec.Version
                    && p.TenantId == tenantId,
                ct);

            if (tenantOverride is null)
            {
                tenantOverride = CloneForTenant(spec, tenantId);
                _dbContext.PromptSpecs.Add(tenantOverride);
            }

            ApplyUpdates(tenantOverride, request);
            await _dbContext.SaveChangesAsync(ct);
            return MapToResponse(tenantOverride);
        }

        ApplyUpdates(spec, request);

        await _dbContext.SaveChangesAsync(ct);

        return MapToResponse(spec);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var spec = await _dbContext.PromptSpecs.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"PromptSpec with ID {id} not found.");

        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            if (spec.TenantId is null)
                throw new InvalidOperationException("Global prompt templates cannot be deleted from a tenant context.");

            if (spec.TenantId != tenantId)
                throw new InvalidOperationException("Tenant mismatch detected for the requested prompt template.");
        }

        spec.IsDeleted = true;
        spec.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    private Guid? ResolveTenantScope()
        => _tenantProvider.TryGetCurrentTenantId(out var tenantId) ? tenantId : null;

    private static void ApplyUpdates(PromptSpec spec, UpdatePromptSpecRequest request)
    {
        if (request.SystemTemplate is not null) spec.SystemTemplate = request.SystemTemplate;
        if (request.UserTemplate is not null) spec.UserTemplate = request.UserTemplate;
        if (request.DeveloperTemplate is not null) spec.DeveloperTemplate = request.DeveloperTemplate;
        if (request.VariablesSchemaJson is not null) spec.VariablesSchemaJson = request.VariablesSchemaJson;
        if (request.OutputSchemaJson is not null) spec.OutputSchemaJson = request.OutputSchemaJson;
        if (request.SafetyPolicyRef is not null) spec.SafetyPolicyRef = request.SafetyPolicyRef;
        if (request.IsPublished.HasValue) spec.IsPublished = request.IsPublished.Value;
    }

    private static PromptSpec CloneForTenant(PromptSpec source, Guid tenantId) => new()
    {
        TenantId = tenantId,
        Name = source.Name,
        Version = source.Version,
        SystemTemplate = source.SystemTemplate,
        UserTemplate = source.UserTemplate,
        DeveloperTemplate = source.DeveloperTemplate,
        VariablesSchemaJson = source.VariablesSchemaJson,
        OutputSchemaJson = source.OutputSchemaJson,
        SafetyPolicyRef = source.SafetyPolicyRef,
        IsPublished = source.IsPublished,
    };

    private static PromptSpecResponse MapToResponse(PromptSpec spec) => new()
    {
        Id = spec.Id,
        TenantId = spec.TenantId,
        Name = spec.Name,
        Version = spec.Version,
        SystemTemplate = spec.SystemTemplate,
        UserTemplate = spec.UserTemplate,
        DeveloperTemplate = spec.DeveloperTemplate,
        VariablesSchemaJson = spec.VariablesSchemaJson,
        OutputSchemaJson = spec.OutputSchemaJson,
        SafetyPolicyRef = spec.SafetyPolicyRef,
        IsPublished = spec.IsPublished,
        CreatedAt = spec.CreatedAt,
        UpdatedAt = spec.UpdatedAt,
    };
}
