using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Usage;

/// <summary>
/// Committed consumption (Spec 087 §9) — the durable fact a hold becomes.
/// </summary>
public class UsageRecord : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string SubscriberKind { get; set; } = string.Empty;

    public Guid SubscriberId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string MeterCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>
    /// Which grants this drew on and by how much. Without it a refund or a dispute cannot be
    /// unwound and breakage cannot be computed.
    /// </summary>
    public string AllocationsJson { get; set; } = "[]";

    public DateTime OccurredAt { get; set; }

    /// <summary>What caused the usage — for AI work, the <c>AiRun</c> id, which keeps that audit record as the join.</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    /// <summary>What the work actually cost us. The other half of the margin figure.</summary>
    public decimal? ProviderCost { get; set; }

    public string? ProviderCostCurrency { get; set; }
}
