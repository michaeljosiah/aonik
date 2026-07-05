using Aonik.PersonalFinance.Services;
using FluentAssertions;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 044 §5 — the roll-forward math in one tested place: anchor-day clamp,
/// leap year, termly explicit dates, one-off no-roll.
/// </summary>
public class RhythmTests
{
    [Fact]
    public void Monthly_Anchor28_AdvancesToSameDayNextMonth()
    {
        var rhythm = new Rhythm("Monthly", 1, 28, null);
        rhythm.NextAfter(new DateTime(2026, 5, 28)).Should().Be(new DateTime(2026, 6, 28));
    }

    [Fact]
    public void Monthly_Anchor31_ClampsToShortMonthEnd()
    {
        var rhythm = new Rhythm("Monthly", 1, 31, null);
        rhythm.NextAfter(new DateTime(2026, 1, 31)).Should().Be(new DateTime(2026, 2, 28)); // 2026 not leap
        rhythm.NextAfter(new DateTime(2026, 3, 31)).Should().Be(new DateTime(2026, 4, 30));
    }

    [Fact]
    public void Monthly_Anchor29_LeapFebruary_Gives29Feb()
    {
        var rhythm = new Rhythm("Monthly", 1, 29, null);
        rhythm.NextAfter(new DateTime(2028, 1, 29)).Should().Be(new DateTime(2028, 2, 29)); // 2028 leap
    }

    [Fact]
    public void Weekly_Interval2_AddsFortnight()
    {
        var rhythm = new Rhythm("Weekly", 2, null, null);
        rhythm.NextAfter(new DateTime(2026, 5, 1)).Should().Be(new DateTime(2026, 5, 15));
    }

    [Fact]
    public void Quarterly_AddsThreeMonths()
    {
        var rhythm = new Rhythm("Quarterly", 1, null, null);
        rhythm.NextAfter(new DateTime(2026, 1, 15)).Should().Be(new DateTime(2026, 4, 15));
    }

    [Fact]
    public void Yearly_AddsTwelveMonths()
    {
        var rhythm = new Rhythm("Yearly", 1, null, null);
        rhythm.NextAfter(new DateTime(2026, 9, 1)).Should().Be(new DateTime(2027, 9, 1));
    }

    [Fact]
    public void Termly_ReturnsNextExplicitDate_NotComputedRoll()
    {
        var terms = new List<DateTime> { new(2026, 1, 10), new(2026, 4, 20), new(2026, 9, 5) };
        var rhythm = new Rhythm("Termly", 1, null, terms);
        rhythm.NextAfter(new DateTime(2026, 1, 10)).Should().Be(new DateTime(2026, 4, 20));
        rhythm.NextAfter(new DateTime(2026, 4, 20)).Should().Be(new DateTime(2026, 9, 5));
    }

    [Fact]
    public void Termly_AfterLastDate_ReturnsNull()
    {
        var rhythm = new Rhythm("Termly", 1, null, new List<DateTime> { new(2026, 1, 10) });
        rhythm.NextAfter(new DateTime(2026, 1, 10)).Should().BeNull();
    }

    [Fact]
    public void OneOff_ReturnsNull_NoRoll()
    {
        var rhythm = new Rhythm("OneOff", 1, null, new List<DateTime> { new(2026, 6, 1) });
        rhythm.NextAfter(new DateTime(2026, 6, 1)).Should().BeNull();
    }

    [Fact]
    public void Label_RendersHumanRhythm()
    {
        new Rhythm("Monthly", 1, 28, null).Label().Should().Be("Monthly · 28th");
        new Rhythm("Monthly", 1, 1, null).Label().Should().Be("Monthly · 1st");
        new Rhythm("Termly", 1, null, null).Label().Should().Be("Each term");
        new Rhythm("Weekly", 1, null, null).Label().Should().Be("Weekly");
    }
}
