using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiModel : AuditableEntity
{
    public Guid AiModelId { get; set; }
    public Guid AiProviderId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int ContextWindow { get; set; }
    public string CostProfileJson { get; set; } = string.Empty;
    public string LatencyProfileJson { get; set; } = string.Empty;
    public string PolicyTagsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
