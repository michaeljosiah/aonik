namespace Aonik.SharedKernel.Events.Integration;

/// <summary>
/// Raised — through the transactional outbox — when a decision reaches a durable outcome (Spec 041,
/// Addition C): a proposal applied/failed, an invoice paid, a work item run resolved. The Worker picks
/// it up out of band, restores the originating tenant, and feeds the decision-outcome extractor so the
/// platform learns rationale (per-user) and patterns (per-tenant). Carries IDs/references, not PII.
/// </summary>
/// <param name="SourceType">Originating record kind: "Proposal", "WorkItemRun", "Invoice", "Order".</param>
/// <param name="Outcome">Terminal outcome, e.g. "Applied", "Failed", "Paid", "Succeeded".</param>
/// <param name="ContextJson">Optional distilled context (statement, conditions, subject, chosen option). No PII.</param>
public sealed record DecisionResolvedEvent(
    Guid TenantId,
    string DecisionType,
    string SourceType,
    Guid SourceId,
    Guid? UserId,
    Guid? AiRunId,
    string Outcome,
    string? Segment,
    string? ContextJson) : IIntegrationEvent;
