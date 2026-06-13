namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// The commitment rhythm value object (Spec 044 §5). All next-due arithmetic
/// lives here — never scattered across services — because roll-forward is the
/// most error-prone surface in the spine. DateTime with date-only semantics
/// (the codebase convention; src has zero DateOnly usage).
/// </summary>
internal sealed record Rhythm(string Unit, int Interval, int? AnchorDay, IReadOnlyList<DateTime>? TermDates)
{
    /// <summary>
    /// The next due date strictly after <paramref name="from"/>, or null for a
    /// OneOff / exhausted Termly schedule.
    ///   Weekly/Monthly/Quarterly/Yearly → from + Interval units, clamping
    ///                                       AnchorDay to month end (31st → 30 Jun / 28 Feb).
    ///   Termly / OneOff                 → the next explicit date in TermDates (no computed roll).
    /// </summary>
    public DateTime? NextAfter(DateTime from)
    {
        var fromDate = from.Date;
        var interval = Interval <= 0 ? 1 : Interval;

        switch ((Unit ?? "Monthly").Trim().ToLowerInvariant())
        {
            case "weekly":
                return fromDate.AddDays(7 * interval);

            case "monthly":
                return AddMonthsClamped(fromDate, interval, AnchorDay);

            case "quarterly":
                return AddMonthsClamped(fromDate, 3 * interval, AnchorDay);

            case "yearly":
                return AddMonthsClamped(fromDate, 12 * interval, AnchorDay);

            case "termly":
            case "oneoff":
                return TermDates?
                    .Select(d => d.Date)
                    .Where(d => d > fromDate)
                    .OrderBy(d => d)
                    .Cast<DateTime?>()
                    .FirstOrDefault();

            default:
                return AddMonthsClamped(fromDate, interval, AnchorDay);
        }
    }

    /// <summary>
    /// A human label for the unified projection ("Monthly · 28th", "Each term").
    /// </summary>
    public string Label()
    {
        var interval = Interval <= 0 ? 1 : Interval;
        var unit = (Unit ?? "Monthly").Trim();

        return unit.ToLowerInvariant() switch
        {
            "termly" => "Each term",
            "oneoff" => "One-off",
            "monthly" when AnchorDay is int day => interval == 1
                ? $"Monthly · {Ordinal(day)}"
                : $"Every {interval} months · {Ordinal(day)}",
            _ => interval == 1 ? unit : $"Every {interval} {unit.ToLowerInvariant()}",
        };
    }

    private static DateTime AddMonthsClamped(DateTime from, int months, int? anchorDay)
    {
        var shifted = from.AddMonths(months);
        var day = anchorDay ?? from.Day;
        var daysInMonth = DateTime.DaysInMonth(shifted.Year, shifted.Month);
        return new DateTime(shifted.Year, shifted.Month, Math.Min(day, daysInMonth));
    }

    private static string Ordinal(int day)
    {
        if (day is >= 11 and <= 13)
        {
            return $"{day}th";
        }

        return (day % 10) switch
        {
            1 => $"{day}st",
            2 => $"{day}nd",
            3 => $"{day}rd",
            _ => $"{day}th",
        };
    }
}
