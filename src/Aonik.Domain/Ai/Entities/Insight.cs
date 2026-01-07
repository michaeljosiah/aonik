using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class Insight : Entity
{
    public string SubjectType { get; private set; } = string.Empty;
    public Guid SubjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public DateTime CreatedUtc { get; private set; }

    private Insight() { }

    public Insight(string subjectType, Guid subjectId, string title, string summary)
    {
        SubjectType = subjectType;
        SubjectId = subjectId;
        Title = title;
        Summary = summary;
        CreatedUtc = DateTime.UtcNow;
    }
}
