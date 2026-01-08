using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class ToolSpec : AuditableEntity
{
    public Guid ToolSpecId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ContractJson { get; set; } = string.Empty;
    public string? AuthScope { get; set; }
    public string RateLimitsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
