using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiFeedback : AuditableEntity
{
    public Guid AiFeedbackId { get; private set; }
    public Guid AiRunId { get; private set; }
    public int Rating { get; private set; }
    public string? Correction { get; private set; }
    public string? GroundTruthRef { get; private set; }

    private AiFeedback() { }

    public AiFeedback(Guid aiRunId, int rating)
    {
        AiFeedbackId = Id;
        AiRunId = aiRunId;
        Rating = rating;
    }

    public void UpdateRating(int rating)
    {
        Rating = rating;
    }

    public void AddCorrection(string correction)
    {
        Correction = correction;
    }

    public void AddGroundTruthRef(string groundTruthRef)
    {
        GroundTruthRef = groundTruthRef;
    }
}
