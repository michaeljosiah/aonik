namespace Aonik.Ai.Services;

/// <summary>
/// Thrown by <see cref="AiRunWriter"/> when an AI run is attempted while the
/// tenant's kill switch is engaged. Callers (chat surfaces, agent runners,
/// insight pipelines) should catch this to surface a friendly "agents
/// paused" message rather than letting it bubble as a generic 500.
/// </summary>
public sealed class KillSwitchEngagedException : Exception
{
    public Guid TenantId { get; }
    public DateTime? EngagedAt { get; }
    public Guid? EngagedByUserId { get; }

    public KillSwitchEngagedException(
        Guid tenantId,
        DateTime? engagedAt,
        Guid? engagedByUserId)
        : base($"AI runs are paused for tenant {tenantId} — kill switch is engaged.")
    {
        TenantId = tenantId;
        EngagedAt = engagedAt;
        EngagedByUserId = engagedByUserId;
    }
}
