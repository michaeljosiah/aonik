namespace Aonik.SharedKernel.Abstractions.Subscriptions;

// Spec 087 — the cross-module contract surface for subscriptions, entitlements and metered usage.
// These records carry only what consumers need; the Subscriptions entities never cross the boundary.

/// <summary>
/// Who holds a subscription or an entitlement. <paramref name="Kind"/> is an open string from
/// <see cref="SubscriberKinds"/>; <paramref name="Id"/> is opaque to the Subscriptions module,
/// which stores it and never dereferences it. Existence and authority are established per kind
/// through <see cref="ISubscriberAuthorizer"/>.
/// </summary>
public sealed record SubscriberRef(string Kind, Guid Id);

/// <summary>One meter's standing for a subscriber, as a product's UI would render it.</summary>
/// <param name="MeterCode">The tenant-scoped meter code.</param>
/// <param name="Kind">One of <see cref="MeterKinds"/>. Owned by the meter, not the plan entitlement.</param>
/// <param name="Unit">Display unit, e.g. "stories". Null for flags.</param>
/// <param name="Allowance">Total granted. For a ceiling this is the maximum; for a flag, 1 when on.</param>
/// <param name="Consumed">Spent (counter) or currently held (ceiling).</param>
/// <param name="Held">Reserved but not yet committed. Always zero for ceilings and flags.</param>
/// <param name="Remaining"><c>Allowance - Consumed - Held</c>, floored at zero.</param>
/// <param name="ResetPolicy">One of <see cref="ResetPolicies"/>.</param>
/// <param name="ResetsAt">When the perishable part of this allowance lapses; null when it never does.</param>
public sealed record MeterEntitlement(
    string MeterCode,
    string Kind,
    string? Unit,
    decimal Allowance,
    decimal Consumed,
    decimal Held,
    decimal Remaining,
    string ResetPolicy,
    DateTime? ResetsAt);

/// <summary>Everything a product needs to show "what do I have, and how much of it is left".</summary>
/// <remarks>
/// The subscription fields are <b>nullable</b>, and that is the point rather than a convenience.
/// Purchased grants are keyed to the <em>subscriber</em> precisely so they outlive subscriptions
/// (§8): someone who buys a standalone top-up, or who cancels while still holding one, has real
/// drawable allowance and no subscription at all. Making these required forced this reader to answer
/// null for them, so a product using it as the documented pre-check refused work that
/// <c>IUsageMeter</c> would have funded — allowance the customer had already paid for.
/// </remarks>
public sealed record EntitlementSnapshot(
    SubscriberRef Subscriber,
    Guid? SubscriptionId,
    string PlanCode,
    string PlanName,
    Guid? PlanVersionId,
    string? Status,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    IReadOnlyList<MeterEntitlement> Meters);

/// <summary>A subscription as seen from outside the module.</summary>
/// <param name="PendingPlanCode">A plan change accepted but not yet in force; applied only when the next period settles.</param>
public sealed record SubscriptionDto(
    Guid Id,
    SubscriberRef Subscriber,
    string PlanCode,
    Guid PlanVersionId,
    string? PendingPlanCode,
    DateTime? PendingEffectiveAt,
    string Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    Guid? PaymentMandateId,
    DateTime StartedAt,
    DateTime? EndedAt);

/// <summary>A hold on allowance, taken before metered work runs.</summary>
/// <param name="ExpiresAt">When the sweep will return the hold if it is neither committed nor released.</param>
public sealed record UsageReservationRef(
    Guid ReservationId,
    string MeterCode,
    decimal Quantity,
    DateTime ExpiresAt);

/// <summary>What caused a unit of usage — for an AI dispatch, the <c>AiRun</c> id.</summary>
/// <param name="ProviderCost">What the work actually cost us, when known. The other half of the margin figure.</param>
public sealed record UsageSource(
    string SourceType,
    Guid SourceId,
    decimal? ProviderCost = null,
    string? ProviderCostCurrency = null);

/// <summary>The outcome of converting a hold into consumption.</summary>
/// <param name="Allocations">Which grants were drawn on, and by how much. Required for refunds and breakage.</param>
public sealed record UsageCommitResult(
    Guid UsageRecordId,
    decimal QuantityCommitted,
    IReadOnlyList<GrantAllocation> Allocations);

/// <summary>One grant's share of a reservation or a committed usage record.</summary>
/// <param name="UnitValue">
/// What one unit of this grant was paid for, when it was purchased. Carried so revenue is
/// recognised at the price actually charged rather than an estimate (Spec 087 O7). Null for plan
/// allowance, which carries no deferred-revenue liability.
/// </param>
public sealed record GrantAllocation(
    Guid GrantId,
    string Source,
    decimal Quantity,
    DateTime? GrantExpiresAt,
    decimal? UnitValue = null,
    string? UnitValueCurrency = null);

/// <summary>A meter a module owns, contributed at provisioning. See <see cref="IMeterDefinitionProvider"/>.</summary>
public sealed record MeterDefinition(string Code, string DisplayName, string Kind, string? Unit = null);

/// <summary>The order raised for an entitlement top-up, priced server-side from a <c>MeterOffer</c>.</summary>
public sealed record EntitlementPurchaseRef(
    Guid OrderId,
    string MeterCode,
    decimal Quantity,
    decimal Total,
    string Currency);
