using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class Signal : Entity
{
    public string Type { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTime CreatedUtc { get; private set; }

    private Signal() { }

    public Signal(string type, string severity, string message)
    {
        Type = type;
        Severity = severity;
        Message = message;
        CreatedUtc = DateTime.UtcNow;
    }
}
