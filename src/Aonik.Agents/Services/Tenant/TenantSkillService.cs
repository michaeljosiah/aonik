using System.Text;
using System.Text.Json;
using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services.Tenant;

/// <summary>
/// Tenant skill management (Spec 033 §8.1, §7.1). Validates + stores uploads, runs the review state
/// machine, and exposes the catalogue. A pure-instruction skill (no scripts) auto-approves on a clean
/// validation; a skill with scripts requires PlatformAdmin review and script enablement.
/// </summary>
public interface ITenantSkillService
{
    Task<IReadOnlyList<TenantSkillDto>> ListAsync(CancellationToken ct = default);
    Task<SkillValidationDto> ValidateAsync(string markdown, CancellationToken ct = default);
    Task<(TenantSkillDto? Dto, SkillValidationDto Validation)> UploadAsync(string markdown, CancellationToken ct = default);
    Task<SkillPreviewDto?> PreviewAsync(Guid id, CancellationToken ct = default);
    Task<TenantSkillDto?> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<TenantSkillDto?> ActivateAsync(Guid id, CancellationToken ct = default);
    Task<TenantSkillDto?> DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TenantSkillDto?> ReviewAsync(Guid id, bool approve, string? notes, CancellationToken ct = default);
    Task<TenantSkillDto?> EnableScriptsAsync(Guid id, bool enabled, string? notes, CancellationToken ct = default);
}

internal sealed class TenantSkillService : ITenantSkillService
{
    private readonly AgentsDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly IFileStore _fileStore;
    private readonly ITenantSkillValidator _validator;
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly IServiceProvider _sp;
    private readonly IClock _clock;

    public TenantSkillService(
        AgentsDbContext db,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        IFileStore fileStore,
        ITenantSkillValidator validator,
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IServiceProvider sp,
        IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _fileStore = fileStore;
        _validator = validator;
        _descriptors = descriptors;
        _sp = sp;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TenantSkillDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.TenantSkills.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SkillValidationDto> ValidateAsync(string markdown, CancellationToken ct = default)
    {
        var result = await _validator.ValidateAsync(markdown, GetAvailableToolNames(), ct).ConfigureAwait(false);
        return new SkillValidationDto(result.IsValid, result.Errors, result.Name, result.Description, result.AllowedTools, result.ScriptsPresent);
    }

    public async Task<(TenantSkillDto? Dto, SkillValidationDto Validation)> UploadAsync(string markdown, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var result = await _validator.ValidateAsync(markdown, GetAvailableToolNames(), ct).ConfigureAwait(false);
        var validationDto = new SkillValidationDto(result.IsValid, result.Errors, result.Name, result.Description, result.AllowedTools, result.ScriptsPresent);
        if (!result.IsValid)
        {
            return (null, validationDto);
        }

        var skillId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        var upload = await _fileStore.UploadAsync(tenantId, skillId, stream, "SKILL.md", "text/markdown", ct).ConfigureAwait(false);

        // Spec 033 §7.1: a skill that adds no new tool and no scripts may auto-approve on clean
        // validation — it carries no capability the agent didn't already have.
        var autoApprove = !result.ScriptsPresent;

        var skill = new TenantSkill
        {
            Id = skillId,
            TenantId = tenantId,
            Name = result.Name,
            Version = result.Version,
            Description = result.Description,
            StorageKey = upload.StorageKey,
            Sha256 = upload.Sha256,
            SizeBytes = upload.FileSizeBytes,
            FrontmatterJson = result.FrontmatterJson,
            AllowedToolsJson = JsonSerializer.Serialize(result.AllowedTools),
            ScriptsPresent = result.ScriptsPresent,
            ScriptsEnabled = false,
            ApprovalState = autoApprove ? TenantExtensionApprovalState.Approved : TenantExtensionApprovalState.Draft,
            IsActive = false,
        };

        _db.TenantSkills.Add(skill);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return (ToDto(skill), validationDto);
    }

    public async Task<SkillPreviewDto?> PreviewAsync(Guid id, CancellationToken ct = default)
    {
        var skill = await _db.TenantSkills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (skill is null)
        {
            return null;
        }

        var markdown = string.Empty;
        if (!string.IsNullOrWhiteSpace(skill.StorageKey))
        {
            try
            {
                await using var stream = await _fileStore.OpenReadAsync(skill.StorageKey, ct).ConfigureAwait(false);
                if (stream is not null)
                {
                    using var reader = new StreamReader(stream);
                    markdown = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                }
            }
            catch
            {
                // best-effort: a missing/unreadable package still previews the catalogue line
            }
        }

        // The progressive-disclosure catalogue line the AgentSkillsProvider injects up-front; the body
        // above is what the model pulls on demand via load_skill.
        var catalogue =
            $"## Available skills\n- **{skill.Name}**: {skill.Description}\n\n" +
            $"Call load_skill(\"{skill.Name}\") to load the full procedure when relevant.";

        return new SkillPreviewDto(skill.Name, skill.Description, ParseTools(skill.AllowedToolsJson), catalogue, markdown);
    }

    public Task<TenantSkillDto?> SubmitAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            if (s.ApprovalState is not (TenantExtensionApprovalState.Draft or TenantExtensionApprovalState.Rejected))
            {
                throw new InvalidOperationException("Only a draft or rejected skill can be submitted for review.");
            }
            s.ApprovalState = TenantExtensionApprovalState.PendingPlatformReview;
        }, ct);

    public Task<TenantSkillDto?> ActivateAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            if (s.ApprovalState != TenantExtensionApprovalState.Approved)
            {
                throw new InvalidOperationException("Only an approved skill can be activated.");
            }
            s.IsActive = true;
        }, ct);

    public Task<TenantSkillDto?> DeactivateAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(id, s => s.IsActive = false, ct);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var skill = await _db.TenantSkills.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (skill is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(skill.StorageKey))
        {
            try { await _fileStore.DeleteAsync(skill.StorageKey, ct).ConfigureAwait(false); } catch { /* best effort */ }
        }

        _db.TenantSkills.Remove(skill);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public Task<TenantSkillDto?> ReviewAsync(Guid id, bool approve, string? notes, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            s.ApprovalState = approve ? TenantExtensionApprovalState.Approved : TenantExtensionApprovalState.Rejected;
            s.ReviewedByUserId = CurrentUserId();
            s.ReviewedAt = _clock.UtcNow;
            s.ReviewNotes = notes;
            if (!approve)
            {
                s.IsActive = false;
            }
        }, ct);

    public Task<TenantSkillDto?> EnableScriptsAsync(Guid id, bool enabled, string? notes, CancellationToken ct = default) =>
        MutateAsync(id, s =>
        {
            s.ScriptsEnabled = enabled;
            s.ReviewedByUserId = CurrentUserId();
            s.ReviewedAt = _clock.UtcNow;
            s.ReviewNotes = notes;
        }, ct);

    private async Task<TenantSkillDto?> MutateAsync(Guid id, Action<TenantSkill> mutate, CancellationToken ct)
    {
        var skill = await _db.TenantSkills.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
        if (skill is null)
        {
            return null;
        }
        mutate(skill);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToDto(skill);
    }

    private IReadOnlyCollection<string> GetAvailableToolNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in _descriptors)
        {
            try
            {
                foreach (var name in descriptor.GetToolNames(_sp))
                {
                    names.Add(name);
                }
            }
            catch
            {
                // A descriptor that can't enumerate its tools just contributes nothing to the ceiling.
            }
        }
        return names;
    }

    private Guid RequireTenant() =>
        _tenant.TryGetCurrentTenantId(out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("A tenant context is required.");

    private Guid? CurrentUserId() => _user.TryGetCurrentUserId(out var id) ? id : null;

    private static TenantSkillDto ToDto(TenantSkill s) => new(
        s.Id, s.Name, s.Version, s.Description, s.ScriptsPresent, s.ScriptsEnabled,
        s.ApprovalState.ToString(), s.IsActive, ParseTools(s.AllowedToolsJson),
        s.CreatedAt, s.ReviewedAt, s.ReviewNotes);

    private static IReadOnlyList<string> ParseTools(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
