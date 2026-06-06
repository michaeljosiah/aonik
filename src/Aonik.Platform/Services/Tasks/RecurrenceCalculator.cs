using Quartz;

namespace Aonik.Platform.Services.Tasks;

/// <summary>
/// Computes recurrence fire times for <c>WorkItem</c>s (Spec 034) from a Quartz
/// cron expression and an optional IANA timezone, using Quartz's
/// <see cref="CronExpression"/> (already a solution dependency) — zero new deps.
/// One-off tasks do not use this; recurring tasks call it for both the first
/// occurrence (at schedule time) and the re-arm (after each dispatch).
/// </summary>
public sealed class RecurrenceCalculator
{
    /// <summary>
    /// Returns the next fire time strictly after <paramref name="afterUtc"/>, in UTC,
    /// or <c>null</c> if the expression has no further occurrences. Returning the
    /// <em>strictly-after</em> instant is what makes re-arm safe — an occurrence never
    /// re-fires itself.
    /// </summary>
    public DateTime? GetNextOccurrenceUtc(string cronExpression, string? timezone, DateTime afterUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

        var cron = new CronExpression(cronExpression)
        {
            TimeZone = ResolveTimeZone(timezone),
        };

        var afterUtcKind = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var next = cron.GetNextValidTimeAfter(new DateTimeOffset(afterUtcKind));
        return next?.UtcDateTime;
    }

    /// <summary>True when <paramref name="cronExpression"/> is a valid Quartz cron expression.</summary>
    public bool IsValidCron(string? cronExpression) =>
        !string.IsNullOrWhiteSpace(cronExpression) && CronExpression.IsValidExpression(cronExpression);

    /// <summary>
    /// Resolves an IANA (or Windows) timezone id to a <see cref="TimeZoneInfo"/>, falling
    /// back to UTC for an unknown/blank id. .NET resolves IANA ids cross-platform.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
