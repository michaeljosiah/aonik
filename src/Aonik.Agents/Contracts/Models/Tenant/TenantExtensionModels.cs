namespace Aonik.Agents.Contracts.Models.Tenant;

// ── Shared ────────────────────────────────────────────────────────────────

/// <summary>A platform review decision (Spec 033 §7.1). Approve clears for activation; reject records a reason.</summary>
public sealed record ReviewDecisionRequest(bool Approve, string? Notes = null);

// ── Skills (Spec 033 §8.1) ─────────────────────────────────────────────────

public sealed record TenantSkillDto(
    Guid Id,
    string Name,
    string Version,
    string Description,
    bool ScriptsPresent,
    bool ScriptsEnabled,
    string ApprovalState,
    bool IsActive,
    IReadOnlyList<string> AllowedTools,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes);

/// <summary>Validate a SKILL.md without persisting (the "validate skill" harness + the upload pre-check).</summary>
public sealed record ValidateSkillRequest(string Markdown);

public sealed record SkillValidationDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    string Name,
    string Description,
    IReadOnlyList<string> AllowedTools,
    bool ScriptsPresent);

/// <summary>Upload a SKILL.md. The name/description/allowed-tools are taken from validated frontmatter.</summary>
public sealed record UploadSkillRequest(string Markdown);

/// <summary>
/// A preview of what a skill contributes to the model's context (Spec 033 §10.2 "preview injected
/// text"): the catalogue entry the <c>AgentSkillsProvider</c> injects up-front, plus the full
/// <c>SKILL.md</c> body the model pulls on demand via <c>load_skill</c>.
/// </summary>
public sealed record SkillPreviewDto(
    string Name,
    string Description,
    IReadOnlyList<string> AllowedTools,
    string CatalogueText,
    string Markdown);

/// <summary>PlatformAdmin toggles a skill's executable scripts (Spec 033 §8.2).</summary>
public sealed record EnableScriptsRequest(bool Enabled, string? Notes = null);

// ── Remote MCP servers (Spec 033 §8.3) ─────────────────────────────────────

public sealed record TenantMcpServerDto(
    Guid Id,
    string Name,
    string Endpoint,
    string TransportType,
    string AuthKind,
    bool AuthConfigured,
    IReadOnlyList<string> AllowedToolPrefixes,
    string DefaultRiskTier,
    string ApprovalState,
    bool IsActive,
    int CredentialVersion,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes);

/// <summary>
/// Create/update a remote MCP server. Auth secrets are write-only: supplied here, encrypted at rest,
/// and never returned. On update, leave <see cref="AuthSecret"/> null to keep the existing secret.
/// </summary>
public sealed record SaveMcpServerRequest(
    string Name,
    string Endpoint,
    string TransportType,
    string AuthKind,
    string? AuthSecret = null,
    string? AuthUsername = null,
    string? AuthHeaderName = null,
    IReadOnlyList<string>? AllowedToolPrefixes = null);

public sealed record McpDiscoveredToolDto(string Name, string Description, string Tier);

public sealed record McpDryRunDto(bool Connected, string? Error, IReadOnlyList<McpDiscoveredToolDto> Tools);

/// <summary>PlatformAdmin review of an MCP server; may set the default risk tier for its mutating tools.</summary>
public sealed record ReviewMcpServerRequest(bool Approve, string? Notes = null, string? DefaultRiskTier = null);

// ── Declarative HTTP / OpenAPI tools (Spec 033 §8.4) ───────────────────────

public sealed record TenantHttpToolDto(
    Guid Id,
    string Name,
    string Description,
    string Method,
    string UrlTemplate,
    string ParameterSchemaJson,
    string AuthKind,
    bool AuthConfigured,
    string RiskTier,
    string? ActionKind,
    string ApprovalState,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes);

public sealed record SaveHttpToolRequest(
    string Name,
    string Description,
    string Method,
    string UrlTemplate,
    string ParameterSchemaJson,
    string AuthKind,
    string? AuthSecret = null,
    string? AuthUsername = null,
    string? AuthHeaderName = null);

public sealed record HttpToolTestDto(string Name, string Tier, string ParameterSchemaJson, string Note);

/// <summary>PlatformAdmin review of an HTTP tool; may set the risk tier (e.g. ReadOnly for a side-effect-free GET).</summary>
public sealed record ReviewHttpToolRequest(bool Approve, string? Notes = null, string? RiskTier = null, string? ActionKind = null, string? ProposalType = null);
