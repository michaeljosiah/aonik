using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

public class AiProvider : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ExternalModelProviderKey { get; set; }
    public string? AuthConfigRef { get; set; }
    public string CapabilitiesJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<AiModel> Models { get; set; } = new();
}
