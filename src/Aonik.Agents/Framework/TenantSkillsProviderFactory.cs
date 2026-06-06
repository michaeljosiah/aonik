using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Builds the current tenant's <see cref="AgentSkillsProvider"/> from its active, approved skills
/// (Spec 033 §8.1). Each skill's <c>SKILL.md</c> is materialised from <see cref="IFileStore"/> to a
/// local directory and parsed by MAF's <c>AgentFileSkillsSource</c>; the builder filters to the
/// enabled set and keeps <c>ScriptApproval</c> on. Returns <see langword="null"/> when there are no
/// active skills, so the descriptor adds no context providers and the agent builds as before.
/// </summary>
internal sealed class TenantSkillsProviderFactory : ITenantSkillsProviderFactory
{
    private readonly TenantSkillMaterializer _materializer;
    private readonly ILogger<TenantSkillsProviderFactory> _logger;

    public TenantSkillsProviderFactory(
        TenantSkillMaterializer materializer,
        ILogger<TenantSkillsProviderFactory> logger)
    {
        _materializer = materializer;
        _logger = logger;
    }

    public AIContextProvider? Create(IServiceProvider serviceProvider)
    {
        try
        {
            return CreateAsync(serviceProvider).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build tenant skills provider; continuing without tenant skills.");
            return null;
        }
    }

    private async Task<AIContextProvider?> CreateAsync(IServiceProvider serviceProvider)
    {
        var tenantProvider = serviceProvider.GetService<ITenantProvider>();
        if (tenantProvider is null || !tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            return null;
        }

        var db = serviceProvider.GetRequiredService<AgentsDbContext>();
        var fileStore = serviceProvider.GetService<IFileStore>();
        if (fileStore is null)
        {
            return null;
        }

        var skills = await db.TenantSkills
            .AsNoTracking()
            .Where(s => s.IsActive && s.ApprovalState == TenantExtensionApprovalState.Approved)
            .ToListAsync()
            .ConfigureAwait(false);

        if (skills.Count == 0)
        {
            return null;
        }

        var dirs = new List<string>(skills.Count);
        var enabledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skills)
        {
            var dir = await _materializer.EnsureMaterializedAsync(tenantId, skill, fileStore).ConfigureAwait(false);
            if (dir is not null)
            {
                dirs.Add(dir);
                enabledNames.Add(skill.Name);
            }
        }

        if (dirs.Count == 0)
        {
            return null;
        }

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var builder = new AgentSkillsProviderBuilder()
            .UseFileSkills(dirs)
            // Enforce the per-tenant enabled set (Spec 033 §8.1).
            .UseFilter(skill => enabledNames.Contains(skill.Frontmatter.Name))
            // Tenant skills always keep ScriptApproval on; a PlatformAdmin enabling scripts is a
            // separate, audited per-skill flag handled before activation.
            .UseScriptApproval(true);

        if (loggerFactory is not null)
        {
            builder = builder.UseLoggerFactory(loggerFactory);
        }

        return builder.Build();
    }
}
