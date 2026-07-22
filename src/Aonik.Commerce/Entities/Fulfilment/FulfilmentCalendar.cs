using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Fulfilment;

/// <summary>
/// Per-tenant fulfilment configuration (Spec 069 §4) — the smallest honest model that makes a
/// delivery promise possible: which days we deliver, when the order book closes, how long
/// preparation takes. At most one per tenant in phase 1 (multi-zone calendars key by zone and
/// belong with phase 2's zone work — O2). Deliberately generic (ADR-013): any make-to-order
/// tenant has this shape. Anemic.
/// </summary>
public class FulfilmentCalendar : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>IANA timezone id, e.g. "Europe/London". All cutoff/date maths happens in it.</summary>
    public string Timezone { get; set; } = "Europe/London";

    /// <summary>JSON array of delivery weekdays, lower-case English day names, e.g.
    /// ["wednesday","thursday"]. Must be non-empty for the calendar to be active.</summary>
    public string DeliveryDaysJson { get; set; } = "[]";

    /// <summary>Local time of day after which an order joins the next cycle, e.g. 12:00.</summary>
    public TimeOnly CutoffLocalTime { get; set; }

    /// <summary>Null = daily order book (the cutoff applies every day). Set (lower-case day
    /// name, e.g. "tuesday") = weekly cycle: the order book closes at that weekday's cutoff, and
    /// orders after it join the FOLLOWING week's cycle. Lead days count from the cycle-close date.</summary>
    public string? CutoffDayOfWeek { get; set; }

    /// <summary>Whole days between the effective order date and the earliest dispatch-eligible
    /// date. 0 = same-day eligible.</summary>
    public int LeadDays { get; set; }

    /// <summary>JSON array of ISO dates ("2026-12-25") on which no delivery happens, whatever
    /// the weekday says. Bounded list; expired entries are pruned on save.</summary>
    public string BlackoutDatesJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
}
