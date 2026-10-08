using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Catalog;

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
    internal const int MaxLeadDays = 60;
    internal const int MaxOfferedDays = 62;
    /// <summary>Beyond the last blackout no blackout can apply, so with ≥1 delivery weekday a
    /// valid day exists within 7 days of the horizon start — a calendar the admin API accepted
    /// can never produce a false null. Exhaustion means genuinely misconfigured.</summary>
    private const int SearchHorizonDays = 62;

    public static DateOnly? EarliestDelivery(FulfilmentCalendar calendar, DateTime nowUtc)
        => ReadRules(calendar) is { } rules ? EarliestDelivery(calendar, nowUtc, rules) : null;

    public static DeliveryDatesDto? DeliveryDates(
        FulfilmentCalendar calendar, DateTime nowUtc, DateOnly? fromDate, int days)
    {
        ValidateRange(fromDate, days);
        var rules = ReadRules(calendar);
        if (rules is null || EarliestDelivery(calendar, nowUtc, rules) is not { } earliest) return null;

        var start = fromDate ?? earliest;
        ValidateRange(start, days);
        var end = start.AddDays(days - 1);
        var dates = new List<DateOnly>();
        for (var day = Math.Max(start.DayNumber, earliest.DayNumber); day <= end.DayNumber; day++)
        {
            var date = DateOnly.FromDayNumber(day);
            if (IsDeliveryDay(date, rules)) dates.Add(date);
        }
        return new DeliveryDatesDto(earliest, calendar.Timezone, start, end, dates);
    }

    internal static void ValidateRange(DateOnly? fromDate, int days)
    {
        if (days is < 1 or > MaxOfferedDays)
            throw new StorefrontValidationException($"days must be between 1 and {MaxOfferedDays}.");
        if (fromDate is { } start && start.DayNumber > DateOnly.MaxValue.DayNumber - (days - 1))
            throw new StorefrontValidationException("The requested delivery date range exceeds the supported dates.");
    }

    private static DateOnly? EarliestDelivery(FulfilmentCalendar calendar, DateTime nowUtc, CalendarRules rules)
    {
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, rules.Timezone);
        var today = DateOnly.FromDateTime(nowLocal);
        int daysToEffectiveDate;
        if (rules.CutoffDay is { } cycleDay)
        {
            // Weekly cycle: the next cycle-close date, or the following week if today's cutoff passed.
            daysToEffectiveDate = ((int)cycleDay - (int)today.DayOfWeek + 7) % 7;
            if (daysToEffectiveDate == 0 && nowUtc > CutoffInstantUtc(today, calendar.CutoffLocalTime, rules.Timezone))
                daysToEffectiveDate = 7;
        }
        else
        {
            // Exactly AT the cutoff still belongs to today's order book.
            daysToEffectiveDate = nowUtc > CutoffInstantUtc(today, calendar.CutoffLocalTime, rules.Timezone) ? 1 : 0;
        }

        var readyDay = today.DayNumber + daysToEffectiveDate + calendar.LeadDays;
        if (readyDay > DateOnly.MaxValue.DayNumber) return null;
        var horizonStart = rules.Blackouts.Count > 0 ? Math.Max(rules.Blackouts.Max().DayNumber, readyDay) : readyDay;
        var horizon = Math.Min(horizonStart + SearchHorizonDays, DateOnly.MaxValue.DayNumber);
        for (var day = readyDay; day <= horizon; day++)
        {
            var date = DateOnly.FromDayNumber(day);
            if (IsDeliveryDay(date, rules)) return date;
        }
        return null;
    }

    private static bool IsDeliveryDay(DateOnly date, CalendarRules rules)
        => rules.DeliveryDays.Contains(date.DayOfWeek) && !rules.Blackouts.Contains(date);

    private static CalendarRules? ReadRules(FulfilmentCalendar calendar)
    {
        if (!calendar.IsActive || calendar.LeadDays is < 0 or > MaxLeadDays) return null;
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(calendar.Timezone);
            var names = JsonSerializer.Deserialize<List<string?>>(calendar.DeliveryDaysJson);
            var rawBlackouts = JsonSerializer.Deserialize<List<string?>>(calendar.BlackoutDatesJson);
            if (names is not { Count: > 0 } || rawBlackouts is null) return null;
            var days = new HashSet<DayOfWeek>();
            foreach (var name in names)
            {
                if (TryParseDay(name) is not { } day) return null;
                days.Add(day);
            }
            var blackouts = new HashSet<DateOnly>();
            foreach (var value in rawBlackouts)
            {
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var date)) return null;
                blackouts.Add(date);
            }
            var cutoffDay = TryParseDay(calendar.CutoffDayOfWeek);
            if (calendar.CutoffDayOfWeek is not null && cutoffDay is null) return null;
            return new CalendarRules(timezone, days, blackouts, cutoffDay);
        }
        catch (Exception ex) when (ex is JsonException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            // Malformed blackout data must not become an empty list and authorize delivery.
            return null;
        }
    }

    private sealed record CalendarRules(TimeZoneInfo Timezone, HashSet<DayOfWeek> DeliveryDays,
        HashSet<DateOnly> Blackouts, DayOfWeek? CutoffDay);

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
                if (DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var date))
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
