using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

public class Insight : Entity
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
