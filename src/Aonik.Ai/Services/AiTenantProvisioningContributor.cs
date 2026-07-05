using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

/// <summary>
/// AI module's contribution to tenant provisioning.
/// Creates default AI route policy.
/// </summary>
internal class AiTenantProvisioningContributor : ITenantProvisioningContributor
{
    private readonly AiDbContext _dbContext;

    public AiTenantProvisioningContributor(AiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => "Ai";

    public async Task<TenantProvisioningContribution> ContributeProvisioningAsync(
        TenantProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        var actions = new List<string>();
        var policiesCreated = 0;

        var existingPolicies = await _dbContext.AiRoutePolicies
            .Where(p => p.TenantId == context.TenantId)
            .ToListAsync(cancellationToken);

        if (existingPolicies.Any())
        {
            actions.Add("AI policies already exist - skipped");
        }
        else
        {
            var defaultPolicy = new AiRoutePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                UseCase = "Default",
                RiskTier = "Low",
                DataSensitivity = "Public",
                PrimaryModelId = Guid.Empty,
                IsActive = true,
                CreatedAt = context.Now,
                CreatedBy = context.UserId
            };

            _dbContext.AiRoutePolicies.Add(defaultPolicy);
            await _dbContext.SaveChangesAsync(cancellationToken);

            actions.Add("Created default AI route policy");
            policiesCreated = 1;
        }

        return new TenantProvisioningContribution(actions, PoliciesCreated: policiesCreated);
    }

    public Task ContributeHealthCheckAsync(
        Guid tenantId,
        List<string> issues,
        CancellationToken cancellationToken = default)
    {
        // AI module doesn't have critical health checks for tenant provisioning
        return Task.CompletedTask;
    }
}
