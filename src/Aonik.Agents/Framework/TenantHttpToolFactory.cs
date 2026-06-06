using System.Net.Http;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Framework;

/// <summary>
/// Turns the current tenant's active, approved <see cref="TenantHttpTool"/> rows (Spec 033 §8.4)
/// into raw <see cref="DeclarativeHttpAIFunction"/> tools, registering each tool's classification in
/// the request-scoped store so the descriptor's single <c>GateAll</c> pass wraps it. No network is
/// touched here — the call happens when the model invokes the tool.
/// </summary>
internal interface ITenantHttpToolFactory
{
    Task<IReadOnlyList<AITool>> CreateToolsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}

internal sealed class TenantHttpToolFactory : ITenantHttpToolFactory
{
    public async Task<IReadOnlyList<AITool>> CreateToolsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var tenantProvider = serviceProvider.GetService<ITenantProvider>();
        if (tenantProvider is null || !tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            return [];
        }

        var db = serviceProvider.GetRequiredService<AgentsDbContext>();
        var protector = serviceProvider.GetRequiredService<ITenantCredentialProtector>();
        var egress = serviceProvider.GetRequiredService<ITenantEgressAllowList>();
        var store = serviceProvider.GetService<ITenantToolClassificationStore>();
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

        var rows = await db.TenantHttpTools
            .AsNoTracking()
            .Where(t => t.IsActive && t.ApprovalState == TenantExtensionApprovalState.Approved)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return [];
        }

        var tools = new List<AITool>(rows.Count);
        foreach (var row in rows)
        {
            store?.Register(row.Name, TenantToolRiskMapping.ClassifyHttpTool(row));
            tools.Add(new DeclarativeHttpAIFunction(row, protector, egress, httpClientFactory));
        }

        return tools;
    }
}
