using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class Signal : Entity
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
