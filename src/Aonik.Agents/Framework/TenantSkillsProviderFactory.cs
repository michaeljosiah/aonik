using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Agents.Framework;

/// <summary>
/// Builds the current tenant's <see cref="AgentSkillsProvider"/> from its active, approved skills
/// (Spec 033 §8.1). Each skill's <c>SKILL.md</c> is materialised from <see cref="IFileStore"/> to a
/// local directory and parsed by MAF's file skill parser; the builder filters to the enabled set and
/// keeps <c>ScriptApproval</c> on. Returns <see langword="null"/> when there are no active skills, so
/// the descriptor adds no context providers and the agent builds as before.
/// <para>
/// Skill scripts are gated (Spec 033 §8.2): a script-bearing skill only has its scripts injected when
/// a PlatformAdmin has enabled them (<see cref="TenantSkill.ScriptsEnabled"/>) AND the deployment
/// allows skill scripts (<see cref="TenantExtensionOptions.AllowSkillScripts"/>). Otherwise the skill
/// is materialised into a scripts-stripped source — its instructions and references still work, but
/// <c>run_skill_script</c> is not exposed — so the "enable scripts" review control is meaningful.
/// </para>
/// </summary>
internal sealed class TenantSkillsProviderFactory : ITenantSkillsProviderFactory
{
    private readonly TenantSkillMaterializer _materializer;
    private readonly IOptionsMonitor<TenantExtensionOptions> _options;
    private readonly ILogger<TenantSkillsProviderFactory> _logger;

    public TenantSkillsProviderFactory(
        TenantSkillMaterializer materializer,
        IOptionsMonitor<TenantExtensionOptions> options,
        ILogger<TenantSkillsProviderFactory> logger)
    {
        _materializer = materializer;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Whether a skill's executable scripts may be injected as runnable: only when the skill declares
    /// scripts, a PlatformAdmin has enabled them, AND the deployment allows skill scripts.
    /// </summary>
    internal static bool ScriptsInjectable(bool scriptsPresent, bool scriptsEnabled, bool allowSkillScripts)
        => scriptsPresent && scriptsEnabled && allowSkillScripts;

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

        var allowSkillScripts = _options.CurrentValue.AllowSkillScripts;

        // Split by the script gate: scripts-enabled skills materialise normally (scripts runnable,
        // under ScriptApproval); everything else materialises with scripts stripped so a not-yet-enabled
        // script-bearing skill still contributes its instructions/references but no runnable script.
        var plainDirs = new List<string>();
        var scriptDirs = new List<string>();
        var enabledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skills)
        {
            var dir = await _materializer.EnsureMaterializedAsync(tenantId, skill, fileStore).ConfigureAwait(false);
            if (dir is null)
            {
                continue;
            }

            enabledNames.Add(skill.Name);
            if (ScriptsInjectable(skill.ScriptsPresent, skill.ScriptsEnabled, allowSkillScripts))
            {
                scriptDirs.Add(dir);
            }
            else
            {
                plainDirs.Add(dir);
            }
        }

        if (plainDirs.Count == 0 && scriptDirs.Count == 0)
        {
            return null;
        }

        var builder = new AgentSkillsProviderBuilder();

        if (plainDirs.Count > 0)
        {
            // AllowedScriptExtensions = [] → no file is treated as a runnable script, so run_skill_script
            // is not exposed for these skills even if a future package ships a scripts/ directory.
            var strippedOptions = new AgentFileSkillsSourceOptions { AllowedScriptExtensions = [] };
            builder = builder.UseFileSkills(plainDirs, strippedOptions);
        }

        if (scriptDirs.Count > 0)
        {
            builder = builder.UseFileSkills(scriptDirs);
        }

        builder = builder
            // Enforce the per-tenant enabled set (Spec 033 §8.1).
            .UseFilter(skill => enabledNames.Contains(skill.Frontmatter.Name))
            // Tenant skills always keep ScriptApproval on — even an enabled script is an approval event.
            .UseScriptApproval(true);

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        if (loggerFactory is not null)
        {
            builder = builder.UseLoggerFactory(loggerFactory);
        }

        return builder.Build();
    }
}
