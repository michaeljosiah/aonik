using System.Text.Json;

using Aonik.Commerce.Entities.Fulfilment;

namespace Aonik.Commerce.Services.Fulfilment;

/// <summary>
/// The Spec 069 §5 computation — deterministic, side-effect-free, clock-injected. All cutoff and
/// date maths happens in the calendar's IANA timezone; comparisons run on UTC instants, which
/// makes the passed-cutoff state monotonic: once the cutoff has passed it stays passed — an
/// autumn clock rolling back from 01:45 to 01:15 must never reopen the order book and move the
/// promise backward.
/// </summary>
internal static class FulfilmentPromiseCalculator
{
    /// <summary>Beyond the last blackout no blackout can apply, so with ≥1 delivery weekday a
    /// valid day exists within 7 days of the horizon start — a calendar the admin API accepted
    /// can never produce a false null. Exhaustion means genuinely misconfigured.</summary>
    private const int SearchHorizonDays = 62;

    public static DateOnly? EarliestDelivery(FulfilmentCalendar calendar, DateTime nowUtc)
    {
        if (!calendar.IsActive)
        {
            return null;
        }

        var deliveryDays = ParseDays(calendar.DeliveryDaysJson);
        if (deliveryDays.Count == 0)
        {
            return null;
        }

        TimeZoneInfo timezone;
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(calendar.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;   // unconfigured is a state, not an error — never guess
        }

        var blackouts = ParseDates(calendar.BlackoutDatesJson);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), timezone);
        var today = DateOnly.FromDateTime(nowLocal);

        DateOnly effectiveDate;
        if (TryParseDay(calendar.CutoffDayOfWeek) is { } cycleDay)
        {
            // Weekly cycle: the order book closes at that weekday's cutoff; orders after it join
            // the FOLLOWING week's cycle. Find the next occurrence whose cutoff has not passed.
            var candidate = today;
            while (candidate.DayOfWeek != cycleDay)
            {
                candidate = candidate.AddDays(1);
            }
            if (candidate == today && nowUtc > CutoffInstantUtc(today, calendar.CutoffLocalTime, timezone))
            {
                candidate = candidate.AddDays(7);
            }
            effectiveDate = candidate;
        }
        else
        {
            // Daily order book: exactly AT the cutoff is still before it — ">" flips the day.
            effectiveDate = nowUtc > CutoffInstantUtc(today, calendar.CutoffLocalTime, timezone)
                ? today.AddDays(1)
                : today;
        }

        var readyDate = effectiveDate.AddDays(calendar.LeadDays);

        var horizonStart = blackouts.Count > 0 && blackouts.Max() > readyDate ? blackouts.Max() : readyDate;
        // Clamp: an extreme stored blackout (e.g. 9999-12-31) must degrade to "no promise", not
        // throw past DateOnly.MaxValue — the upsert bounds new data, but stored data is forever.
        var maxSpan = DateOnly.MaxValue.DayNumber - horizonStart.DayNumber;
        var horizon = horizonStart.AddDays(Math.Min(SearchHorizonDays, maxSpan));
        for (var date = readyDate; date <= horizon; date = date.AddDays(1))
        {
            if (deliveryDays.Contains(date.DayOfWeek) && !blackouts.Contains(date))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>The UTC instant of the FIRST mapping of the wall-clock cutoff on a date (§5 DST
    /// policy): a nonexistent time (spring-forward gap) maps to the first valid instant after
    /// the gap; an ambiguous time (autumn overlap) uses its first occurrence — the earlier UTC
    /// instant, i.e. the larger offset.</summary>
    internal static DateTime CutoffInstantUtc(DateOnly date, TimeOnly cutoffLocal, TimeZoneInfo timezone)
    {
        var local = date.ToDateTime(cutoffLocal, DateTimeKind.Unspecified);

        if (timezone.IsInvalidTime(local))
        {
            // The first valid instant AFTER the gap — sub-minute components must not survive
            // the walk (01:30:30 maps to the gap end 02:00:00, not 02:00:30): truncate to the
            // minute first; DST gaps are whole-minute aligned, so the walk lands exactly on the
            // gap boundary.
            local = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified);
            do
            {
                local = local.AddMinutes(1);
            }
            while (timezone.IsInvalidTime(local));
            return TimeZoneInfo.ConvertTimeToUtc(local, timezone);
        }

        if (timezone.IsAmbiguousTime(local))
        {
            var firstOccurrenceOffset = timezone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, firstOccurrenceOffset).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timezone);
    }

    internal static IReadOnlyList<string> ParseDayNames(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static HashSet<DayOfWeek> ParseDays(string json)
    {
        var days = new HashSet<DayOfWeek>();
        foreach (var name in ParseDayNames(json))
        {
            if (TryParseDay(name) is { } day)
            {
                days.Add(day);
            }
        }
        return days;
    }

    internal static DayOfWeek? TryParseDay(string? name)
        => name?.Trim().ToLowerInvariant() switch
        {
            "monday" => DayOfWeek.Monday,
            "tuesday" => DayOfWeek.Tuesday,
            "wednesday" => DayOfWeek.Wednesday,
            "thursday" => DayOfWeek.Thursday,
            "friday" => DayOfWeek.Friday,
            "saturday" => DayOfWeek.Saturday,
            "sunday" => DayOfWeek.Sunday,
            _ => null,
        };

    internal static HashSet<DateOnly> ParseDates(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            var dates = new HashSet<DateOnly>();
            foreach (var value in raw)
            {
                if (DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date))
                {
                    dates.Add(date);
                }
            }
            return dates;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
