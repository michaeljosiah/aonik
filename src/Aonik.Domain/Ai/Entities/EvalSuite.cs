using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class EvalSuite : AuditableEntity
{
    public Guid EvalSuiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ScenariosJson { get; set; } = string.Empty;
    public string MetricsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
