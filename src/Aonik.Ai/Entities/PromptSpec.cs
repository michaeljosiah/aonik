using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

public class PromptSpec : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SystemTemplate { get; set; } = string.Empty;
    public string UserTemplate { get; set; } = string.Empty;
    public string DeveloperTemplate { get; set; } = string.Empty;
    public string VariablesSchemaJson { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public string? SafetyPolicyRef { get; set; }
    public bool IsPublished { get; set; }
}
