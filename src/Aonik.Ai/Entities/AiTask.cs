using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

/// <summary>
/// Defines a non-agent LLM task with its prompt templates, variable/output schemas,
/// and task metadata. Absorbs the role previously played by <c>PromptSpec</c> while
/// adding first-class task identity (display name, category, execution mode).
///
/// Supports a two-level override model identical to <see cref="AiRoutePolicy"/>:
/// <list type="bullet">
///   <item><b>Global</b> (<c>TenantId = null</c>): Platform-wide defaults, seeded on startup.</item>
///   <item><b>Tenant</b> (<c>TenantId = guid</c>): Per-tenant overrides for prompt text, model, or metadata.</item>
/// </list>
///
/// Model routing is handled by <see cref="AiRoutePolicy"/> via <see cref="UseCase"/> key match.
/// </summary>
public class AiTask : AuditableEntity
{
    /// <summary>Nullable tenant ID. Null = global default; set = tenant-specific override.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Use-case key that links this task to an <see cref="AiRoutePolicy"/> for model resolution.
    /// E.g. "personal_finance_customer_insight_summary".
    /// </summary>
    public string UseCase { get; set; } = string.Empty;

    /// <summary>Human-readable name shown in the Admin UI. E.g. "Customer Insight Summary".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Longer description of what the task does and when it runs.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Logical grouping. E.g. "Finance", "Platform", "Conversation".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>"Realtime" or "Batch" — indicates how the task is typically invoked.</summary>
    public string ExecutionMode { get; set; } = "Realtime";

    /// <summary>
    /// Prompt identifier used by <see cref="SharedKernel.Abstractions.Ai.IAiTaskProfileResolver"/>
    /// for backward-compatible prompt lookups. E.g. "customer_insight_summary".
    /// </summary>
    public string PromptName { get; set; } = string.Empty;

    /// <summary>Prompt version. E.g. "v1", "v2".</summary>
    public string PromptVersion { get; set; } = "v1";

    /// <summary>System prompt template text. May contain {{VARIABLE}} placeholders.</summary>
    public string SystemTemplate { get; set; } = string.Empty;

    /// <summary>User prompt template text. May contain {{VARIABLE}} placeholders.</summary>
    public string UserTemplate { get; set; } = string.Empty;

    /// <summary>Developer prompt template text (optional, rarely used).</summary>
    public string DeveloperTemplate { get; set; } = string.Empty;

    /// <summary>
    /// JSON describing the template variables. E.g. {"SNAPSHOT_JSON": "Deterministic snapshot as JSON"}.
    /// Used by the playground to render a variables form.
    /// </summary>
    public string VariablesSchemaJson { get; set; } = string.Empty;

    /// <summary>
    /// JSON schema for structured output validation. Used by the playground to validate LLM output.
    /// </summary>
    public string OutputSchemaJson { get; set; } = string.Empty;

    /// <summary>Controls whether this task's prompts are resolved at runtime. Unpublished tasks are skipped.</summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>Admin toggle to enable/disable the task.</summary>
    public bool IsActive { get; set; } = true;
}
