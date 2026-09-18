using Aonik.SharedKernel.Abstractions.Subscriptions;

namespace Aonik.Subscriptions.Endpoints.Usage;

// The wire shapes of the metered-usage endpoints (Spec 087 §7; aonik#326 for ArkeKidz#9): a
// reservation held against a subscriber's allowance before paid work starts, committed for what was
// actually used when it finishes, released when it does not. Every call is keyed by the caller's
// own idempotency key or the reservation id, so a lost response is retried without a second charge.

public sealed record SubscriberModel(string Kind, Guid Id);

/// <param name="Subscriber">Whose allowance: a party, or a group the caller may act for.</param>
/// <param name="Quantity">The most the work could consume; the commit says what it did.</param>
/// <param name="IdempotencyKey">The caller's key for this piece of work. The same key returns the same reservation.</param>
/// <param name="HoldForSeconds">How long the hold stands before it lapses on its own; the meter's default when omitted.</param>
public sealed record ReserveUsageRequest(
    SubscriberModel Subscriber,
    string MeterCode,
    decimal Quantity,
    string IdempotencyKey,
    int? HoldForSeconds = null);

public sealed record UsageReservationResponse(
    Guid ReservationId,
    SubscriberModel Subscriber,
    string MeterCode,
    decimal Quantity,
    string Status,
    DateTime ExpiresAt)
{
    public static UsageReservationResponse From(UsageReservationState state) => new(
        state.ReservationId,
        new SubscriberModel(state.Subscriber.Kind, state.Subscriber.Id),
        state.MeterCode,
        state.Quantity,
        state.Status,
        state.ExpiresAt);
}

/// <param name="ActualQuantity">What the work consumed; at most what was reserved. The rest of the hold is returned.</param>
/// <param name="SourceType">What kind of thing the usage was for, e.g. <c>chapter-draft</c>.</param>
/// <param name="SourceId">Which one — the product's own identity for it.</param>
public sealed record CommitUsageRequest(
    decimal ActualQuantity,
    string SourceType,
    Guid SourceId,
    decimal? ProviderCost = null,
    string? ProviderCostCurrency = null);

/// <param name="Replayed">True when the reservation was already committed: the earlier commit stands and nothing was charged again.</param>
public sealed record UsageCommitResponse(
    Guid ReservationId,
    string Status,
    Guid? UsageRecordId,
    decimal? QuantityCommitted,
    bool Replayed);

public sealed record UsageReleaseResponse(Guid ReservationId, string Status);

public sealed record MeterAllowanceModel(
    string MeterCode,
    string Kind,
    string? Unit,
    decimal Allowance,
    decimal Consumed,
    decimal Held,
    decimal Remaining,
    string ResetPolicy,
    DateTime? ResetsAt);

/// <summary>What a subscriber's plan grants and what is left of it, meter by meter.</summary>
public sealed record AllowanceResponse(
    SubscriberModel Subscriber,
    string PlanCode,
    string PlanName,
    string? Status,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    IReadOnlyList<MeterAllowanceModel> Meters)
{
    public static AllowanceResponse From(EntitlementSnapshot snapshot) => new(
        new SubscriberModel(snapshot.Subscriber.Kind, snapshot.Subscriber.Id),
        snapshot.PlanCode,
        snapshot.PlanName,
        snapshot.Status,
        snapshot.CurrentPeriodStart,
        snapshot.CurrentPeriodEnd,
        [.. snapshot.Meters.Select(m => new MeterAllowanceModel(m.MeterCode, m.Kind, m.Unit, m.Allowance, m.Consumed, m.Held, m.Remaining, m.ResetPolicy, m.ResetsAt))]);
}

/// <summary>Every refusal these endpoints make on purpose: a stable code and a message for a log.</summary>
public sealed record UsageProblem(int Status, string Code, string Message);
