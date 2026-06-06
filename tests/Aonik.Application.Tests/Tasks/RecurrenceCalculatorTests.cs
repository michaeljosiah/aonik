using System;
using Aonik.Platform.Services.Tasks;
using FluentAssertions;
using Xunit;

namespace Aonik.Application.Tests.Tasks;

public sealed class RecurrenceCalculatorTests
{
    private readonly RecurrenceCalculator _calculator = new();

    [Fact]
    public void GetNextOccurrenceUtc_Should_ReturnNextMinuteBoundary_For_EveryMinuteCron()
    {
        var after = new DateTime(2026, 6, 6, 10, 0, 30, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc("0 * * * * ?", timezone: null, after);

        next.Should().Be(new DateTime(2026, 6, 6, 10, 1, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextOccurrenceUtc_Should_ReturnStrictlyAfter_Anchor()
    {
        // Anchored exactly on a fire time: the *next* fire is returned, never the anchor itself
        // (this is what makes recurrence re-arm safe — an occurrence never re-fires itself).
        var onTheMinute = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc("0 * * * * ?", timezone: null, onTheMinute);

        next.Should().Be(new DateTime(2026, 6, 6, 10, 1, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextOccurrenceUtc_Should_HonourTimezone_For_DailyCron()
    {
        // 9am daily in New York. From just after midnight UTC on 2026-06-06, the next 9am ET
        // (EDT = UTC-4 in June) is 13:00 UTC the same day.
        var after = new DateTime(2026, 6, 6, 0, 5, 0, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc("0 0 9 * * ?", "America/New_York", after);

        next.Should().Be(new DateTime(2026, 6, 6, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextOccurrenceUtc_Should_FallBackToUtc_For_UnknownTimezone()
    {
        var after = new DateTime(2026, 6, 6, 0, 5, 0, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc("0 0 9 * * ?", "Not/AZone", after);

        next.Should().Be(new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("0 * * * * ?", true)]
    [InlineData("0 0 9 * * ?", true)]
    [InlineData("not a cron", false)]
    [InlineData("* * * *", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCron_Should_Validate(string? cron, bool expected)
    {
        _calculator.IsValidCron(cron).Should().Be(expected);
    }
}
