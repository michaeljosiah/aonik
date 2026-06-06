using System.Collections.Concurrent;
using Aonik.Agents.Entities;
using Aonik.SharedKernel.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Materialises a tenant skill's <c>SKILL.md</c> package from <see cref="IFileStore"/> to a local
/// working directory so MAF's file-based skill parser (<c>AgentFileSkillsSource</c>) can read it
/// (Spec 033 §8.1). Caching is keyed by <c>(tenantId, skillId, sha)</c>: a re-upload changes the
/// SHA and re-materialises; an unchanged skill is written once. Singleton so the on-disk cache
/// survives across requests.
/// </summary>
internal sealed class TenantSkillMaterializer
{
    private const string SkillFileName = "SKILL.md";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ILogger<TenantSkillMaterializer> _logger;
    private readonly string _baseDirectory;

    public TenantSkillMaterializer(ILogger<TenantSkillMaterializer> logger)
    {
        _logger = logger;
        _baseDirectory = Path.Combine(Path.GetTempPath(), "aonik-tenant-skills");
    }

    /// <summary>
    /// Ensure the skill's <c>SKILL.md</c> is on disk and return the containing directory, or
    /// <see langword="null"/> if the package could not be read.
    /// </summary>
    public async Task<string?> EnsureMaterializedAsync(
        Guid tenantId,
        TenantSkill skill,
        IFileStore fileStore,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(skill.StorageKey))
        {
            return null;
        }

        var shaFragment = string.IsNullOrWhiteSpace(skill.Sha256)
            ? "nosha"
            : skill.Sha256[..Math.Min(16, skill.Sha256.Length)];
        var skillDir = Path.Combine(_baseDirectory, tenantId.ToString("N"), $"{skill.Id:N}_{shaFragment}");
        var skillFile = Path.Combine(skillDir, SkillFileName);

        if (File.Exists(skillFile))
        {
            return skillDir;
        }

        var gate = _locks.GetOrAdd(skillFile, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(skillFile))
            {
                return skillDir;
            }

            await using var source = await fileStore.OpenReadAsync(skill.StorageKey, cancellationToken).ConfigureAwait(false);
            if (source is null)
            {
                _logger.LogWarning("Tenant skill {SkillId} package not found at storage key {Key}", skill.Id, skill.StorageKey);
                return null;
            }

            Directory.CreateDirectory(skillDir);
            var tempFile = skillFile + ".tmp";
            await using (var dest = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(dest, cancellationToken).ConfigureAwait(false);
            }
            File.Move(tempFile, skillFile, overwrite: true);

            return skillDir;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to materialise tenant skill {SkillId}", skill.Id);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }
}
