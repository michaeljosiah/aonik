using System.Net.Http;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        var logger = serviceProvider.GetService<ILogger<TenantHttpToolFactory>>();

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
            // Defense-in-depth: a non-conforming tool name would break serialization of the whole
            // agent tool list. Names are rejected at create/update, but skip any that slipped through
            // (e.g. a legacy row) so one bad tool can't take down every request for the tenant.
            if (!ToolNameRules.IsValid(row.Name))
            {
                logger?.LogWarning("Skipping tenant HTTP tool {Id}: '{Name}' is not a valid tool name.", row.Id, row.Name);
                continue;
            }

            store?.Register(row.Name, TenantToolRiskMapping.ClassifyHttpTool(row));
            tools.Add(new DeclarativeHttpAIFunction(row, protector, egress, httpClientFactory));
        }

        return tools;
    }
}
