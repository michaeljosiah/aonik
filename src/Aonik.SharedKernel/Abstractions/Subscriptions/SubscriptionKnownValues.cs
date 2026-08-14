namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// Which kind of thing holds a subscription (Spec 087 §11). An open string, like
/// <c>OrderType</c> and <c>BusinessType</c>, so a new subscriber shape is additive; these
/// constants are only the known values. Resolution and authorisation are contributed per kind
/// via <see cref="ISubscriberAuthorizer"/> — an unregistered kind fails closed.
/// </summary>
public static class SubscriberKinds
{
    /// <summary>A group (Spec 086) — e.g. a family on a consumer product, in a one-tenant-many-families model.</summary>
    public const string Group = "group";

    /// <summary>An individual party.</summary>
    public const string Party = "party";

    /// <summary>The tenant itself — a B2B tenant on a platform plan.</summary>
    public const string Tenant = "tenant";
}

/// <summary>
/// The three shapes an entitlement can take (Spec 087 §5). The kind is owned by the tenant
/// <c>Meter</c> row and is <b>not</b> duplicated onto a plan entitlement — one authority per fact.
/// </summary>
public static class MeterKinds
{
    /// <summary>An allowance drawn down by usage; may reset per period. Uses the reserve/commit lifecycle.</summary>
    public const string Counter = "counter";

    /// <summary>A maximum held concurrently. Checked and claimed, never consumed — deleting the held object returns the slot.</summary>
    public const string Ceiling = "ceiling";

    /// <summary>A capability that is simply on or off.</summary>
    public const string Flag = "flag";
}

/// <summary>
/// When a <see cref="MeterKinds.Counter"/> allowance refreshes (Spec 087 §8). A grant's expiry is
/// derived from this, <b>not</b> from its source — a <see cref="Never"/> plan entitlement
/// accumulates across renewals rather than being discarded at each period end.
/// </summary>
public static class ResetPolicies
{
    /// <summary>Refreshes each billing period; the grant expires at period end.</summary>
    public const string Period = "period";

    /// <summary>Accumulates; the grant never expires.</summary>
    public const string Never = "never";
}

/// <summary>Lifecycle of a subscription (Spec 087 §7).</summary>
public static class SubscriptionStatuses
{
    public const string Trialing = "trialing";
    public const string Active = "active";
    public const string PastDue = "past_due";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";

    /// <summary>
    /// The statuses that occupy the "one active subscription per subscriber" slot. This is the set
    /// the filtered unique index in Spec 087 §17.1 is defined over, so it must stay in lockstep
    /// with that index — a subscriber may hold any number of <see cref="Cancelled"/> or
    /// <see cref="Expired"/> subscriptions, but only one of these.
    /// </summary>
    /// <summary>
    /// The same statuses as <see cref="OccupiesActiveSlot"/>, as an array <strong>for use inside EF
    /// queries</strong>. The relational provider translates array <c>Contains</c> to <c>IN</c>;
    /// <c>IReadOnlySet.Contains</c> it refuses to translate at all — a failure InMemory never surfaces,
    /// which is exactly the class of gap the SQL Server test lane exists for.
    /// </summary>
    public static readonly string[] OccupiesActiveSlotQueryable = ["trialing", "active", "past_due"];

    public static readonly IReadOnlySet<string> OccupiesActiveSlot =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Trialing, Active, PastDue };

    /// <summary>True when the status no longer renews and cannot return to service.</summary>
    public static bool IsTerminal(string? status) =>
        string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Expired, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Where an entitlement grant came from (Spec 087 §8), which determines whether it expires.</summary>
public static class GrantSources
{
    /// <summary>Materialised when a billing period settles. Expires per the entitlement's reset policy.</summary>
    public const string Plan = "plan";

    /// <summary>Bought outright through an entitlement-purchase order. Never expires, and carries a deferred-revenue liability until consumed.</summary>
    public const string Purchase = "purchase";

    /// <summary>Granted by an operator, with a reason.</summary>
    public const string Adjustment = "adjustment";
}

/// <summary>Whether a grant is still drawable, or closed out by the expiry sweep (Spec 087 §8).</summary>
public static class GrantStatuses
{
    public const string Open = "open";
    public const string Closed = "closed";
}

/// <summary>Lifecycle of a usage hold (Spec 087 §9).</summary>
public static class UsageReservationStatuses
{
    /// <summary>Allowance is held against specific grants; not yet spent.</summary>
    public const string Held = "held";

    /// <summary>Converted into consumption.</summary>
    public const string Committed = "committed";

    /// <summary>Returned by the caller; nothing was charged.</summary>
    public const string Released = "released";

    /// <summary>Returned by the sweep after the hold's deadline passed.</summary>
    public const string Expired = "expired";
}

/// <summary>Lifecycle of one billed period (Spec 087 §7).</summary>
public static class SubscriptionPeriodStatuses
{
    public const string Pending = "pending";
    public const string Settled = "settled";
    public const string Failed = "failed";
}

/// <summary>How often a plan bills (Spec 087 §6). An open string; new intervals are additive.</summary>
public static class BillingIntervals
{
    public const string Month = "month";
    public const string Year = "year";

    /// <summary>A plan that never bills — the free tier. Its periods settle without payment or ledger postings.</summary>
    public const string None = "none";
}

/// <summary>Whether a plan is offerable (Spec 087 §6).</summary>
public static class PlanStatuses
{
    /// <summary>Being authored; cannot be subscribed to.</summary>
    public const string Draft = "draft";

    /// <summary>Offerable to new subscribers.</summary>
    public const string Active = "active";

    /// <summary>Withdrawn from sale. Existing subscribers keep it and continue to renew.</summary>
    public const string Retired = "retired";
}

/// <summary>
/// Whether a plan version may still be edited (Spec 087 §6). Only <see cref="Draft"/> is mutable —
/// once published, price and entitlements are frozen, because a subscription pins the version and
/// editing it would re-price everyone on it.
/// </summary>
public static class PlanVersionStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";

    /// <summary>Replaced by a newer published version. Still pinned by any subscription that has it.</summary>
    public const string Superseded = "superseded";
}
