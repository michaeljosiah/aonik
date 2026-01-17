using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiFeedback : AuditableEntity
{
    public Guid AiRunId { get; set; }
    public int Rating { get; set; }
    public string? Correction { get; set; }
    public string? GroundTruthRef { get; set; }
}
