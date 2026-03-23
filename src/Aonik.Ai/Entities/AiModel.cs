using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

public class AiModel : AuditableEntity
{
    public Guid AiProviderId { get; set; }
    public string? ExternalModelKey { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int ContextWindow { get; set; }
    public string CostProfileJson { get; set; } = string.Empty;
    public string LatencyProfileJson { get; set; } = string.Empty;
    public string PolicyTagsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
