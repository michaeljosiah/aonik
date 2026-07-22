namespace Aonik.Commerce.Contracts.Models.Fulfilment;

/// <summary>The promise is a DATE, not a timestamp (Spec 069 §5) — the storefront formats it and
/// derives the weekday label from the date itself, never a second configured string.</summary>
public record FulfilmentPromiseDto(DateOnly EarliestDeliveryDate, string Timezone);

public record FulfilmentCalendarDto(
    string Timezone,
    IReadOnlyList<string> DeliveryDays,
    TimeOnly CutoffLocalTime,
    string? CutoffDayOfWeek,
    int LeadDays,
    IReadOnlyList<string> BlackoutDates,
    bool IsActive,
    /// The promise this calendar computes right now — the upsert response echoes it so the
    /// operator sees the effect immediately (A5). Null when unresolvable.
    FulfilmentPromiseDto? CurrentPromise);

/// <summary>Full replace of the tenant's calendar (Spec 069 §6). Expired blackout dates are
/// pruned on save; at most 100 future dates (A9).</summary>
public record UpsertFulfilmentCalendarCommand(
    string Timezone,
    IReadOnlyList<string> DeliveryDays,
    TimeOnly CutoffLocalTime,
    string? CutoffDayOfWeek,
    int LeadDays,
    IReadOnlyList<string> BlackoutDates,
    bool IsActive);
