using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A tenant-uploaded MAF Agent Skill (Spec 033 §8.1): a <c>SKILL.md</c> package (YAML
/// frontmatter + optional <c>references/</c>, <c>assets/</c>, <c>scripts/</c>) that teaches an
/// agent a procedure via progressive disclosure. The package bytes live in <c>IFileStore</c>
/// under <see cref="StorageKey"/>; this row holds the validated metadata and review state.
/// <para>
/// A skill adds <em>no new tool</em> — its <c>allowed-tools</c> is intersected down to the
/// agent's existing allow-list at upload (Spec 033 §8.1), so it is procedural knowledge over
/// capability the agent already has, never a back door to new capability.
/// </para>
/// </summary>
public class TenantSkill : AuditableEntity, ITenantScoped
{
    /// <summary>The owning tenant. Enforced by the module tenant query filter.</summary>
    public Guid TenantId { get; set; }

    /// <summary>The skill's <c>name</c> (from frontmatter); unique per tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The skill's <c>compatibility</c> / author version string (free-form, e.g. "1.0.0").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The skill's <c>description</c> (from frontmatter); shown in the catalogue.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary><c>IFileStore</c> storage key for the uploaded <c>SKILL.md</c> package.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>SHA-256 of the stored package, from the upload result (integrity / change detection).</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Stored size of the uploaded package in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>The parsed YAML frontmatter serialized to JSON (name/description/license/compatibility/metadata/allowed-tools).</summary>
    public string FrontmatterJson { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of the tool names this skill may reference, AFTER intersecting the frontmatter's
    /// <c>allowed-tools</c> with the agent's existing allow-list and removing money-moving built-ins.
    /// </summary>
    public string AllowedToolsJson { get; set; } = "[]";

    /// <summary>True when the uploaded package contains a <c>scripts/</c> directory with runnable scripts.</summary>
    public bool ScriptsPresent { get; set; }

    /// <summary>
    /// Whether executable scripts are enabled for this skill. Off by default; flips to true only via
    /// a reviewed, audited PlatformAdmin action (Spec 033 §8.2). Even when enabled, the framework
    /// <c>ScriptApproval</c> hook stays on for tenant skills.
    /// </summary>
    public bool ScriptsEnabled { get; set; }

    /// <summary>Review lifecycle state (Spec 033 §7.1).</summary>
    public TenantExtensionApprovalState ApprovalState { get; set; } = TenantExtensionApprovalState.Draft;

    /// <summary>The PlatformAdmin who last reviewed (approved/rejected/script-enabled) this skill.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>When the last platform review decision was recorded.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reviewer note or rejection reason, surfaced to the tenant.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Whether the tenant has activated the skill (only meaningful once <see cref="ApprovalState"/>
    /// is <see cref="TenantExtensionApprovalState.Approved"/>). Only active skills are injected into agents.
    /// </summary>
    public bool IsActive { get; set; }
}
