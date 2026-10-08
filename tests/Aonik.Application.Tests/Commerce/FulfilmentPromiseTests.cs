using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Fulfilment;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 069 §5/§11 — the promise computation (pure, fixed clock) and the calendar service.
/// The A-numbers reference the spec's acceptance table; the worked examples are verbatim.
/// </summary>
public class FulfilmentPromiseTests
{
    private static FulfilmentCalendar Launch(
        string days = """["thursday"]""",
        string cutoff = "12:00",
        string? cutoffDay = null,
        int lead = 14,
        string blackouts = "[]",
        bool active = true,
        string timezone = "Europe/London") => new()
    {
        Timezone = timezone,
        DeliveryDaysJson = days,
        CutoffLocalTime = TimeOnly.Parse(cutoff),
        CutoffDayOfWeek = cutoffDay,
        LeadDays = lead,
        BlackoutDatesJson = blackouts,
        IsActive = active,
    };

    /// <summary>London local → UTC for the BST test dates (UTC+1).</summary>
    private static DateTime Bst(int y, int mo, int d, int h, int mi = 0)
        => new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc).AddHours(-1);

    [Fact]
    public void A1_TheLaunchFixture_ComputesTheDesignDate()
    {
        // Daily cutoff 12:00, lead 14, Thursdays; Tue 2026-07-21 10:00 London → 2026-08-06.
        var result = FulfilmentPromiseCalculator.EarliestDelivery(Launch(), Bst(2026, 7, 21, 10));

        result.Should().Be(new DateOnly(2026, 8, 6));
        new DateOnly(2026, 8, 6).DayOfWeek.Should().Be(DayOfWeek.Thursday, "the weekday derives from the date");
    }

    [Fact]
    public void A2_CrossingTheCutoff_RollsThePromiseAWeek()
    {
        var calendar = Launch();

        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 23, 11))
            .Should().Be(new DateOnly(2026, 8, 6), "11:00 is before cutoff — effective 23 Jul, ready Thu 6 Aug");
        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 23, 12))
            .Should().Be(new DateOnly(2026, 8, 6), "exactly AT the cutoff is still before it — '>' flips the day");
        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 23, 13))
            .Should().Be(new DateOnly(2026, 8, 13), "past cutoff — effective 24 Jul, ready Fri 7 Aug, next Thursday 13 Aug");
    }

    [Fact]
    public void A3_Blackouts_SkipToTheNextDeliveryDay_IncludingConsecutive()
    {
        FulfilmentPromiseCalculator.EarliestDelivery(
                Launch(blackouts: """["2026-08-06"]"""), Bst(2026, 7, 21, 10))
            .Should().Be(new DateOnly(2026, 8, 13));

        FulfilmentPromiseCalculator.EarliestDelivery(
                Launch(blackouts: """["2026-08-06","2026-08-13"]"""), Bst(2026, 7, 21, 10))
            .Should().Be(new DateOnly(2026, 8, 20), "consecutive blackouts each skip a week");
    }

    [Fact]
    public void A8_WeeklyCycle_RollsAFullWeekPastTheCutoff()
    {
        var calendar = Launch(cutoffDay: "tuesday", lead: 2);

        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 20, 10))
            .Should().Be(new DateOnly(2026, 7, 23), "Monday order joins Tuesday's cycle; ready Thu 23 Jul");
        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 21, 13))
            .Should().Be(new DateOnly(2026, 7, 30), "past Tuesday's cutoff the cycle closes NEXT Tuesday — a full week, not one day");
    }

    [Fact]
    public void A4_UnconfiguredStates_ResolveToNoPromise()
    {
        FulfilmentPromiseCalculator.EarliestDelivery(Launch(active: false), Bst(2026, 7, 21, 10))
            .Should().BeNull("inactive is a state, not an error");
        FulfilmentPromiseCalculator.EarliestDelivery(Launch(days: "[]"), Bst(2026, 7, 21, 10))
            .Should().BeNull("no delivery days");
        FulfilmentPromiseCalculator.EarliestDelivery(Launch(timezone: "Not/AZone"), Bst(2026, 7, 21, 10))
            .Should().BeNull("an unresolvable timezone never guesses");
    }

    [Fact]
    public void A6_DstPolicy_FirstMapping_AndMonotonicCutoff()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        // Spring-forward 2026-03-29: 01:30 does not exist — the cutoff maps to the first valid
        // instant after the gap (02:00 BST = 01:00 UTC). Sub-minute components must not survive
        // the walk: 01:30:30 maps to the gap END, not 02:00:30.
        FulfilmentPromiseCalculator.CutoffInstantUtc(new DateOnly(2026, 3, 29), new TimeOnly(1, 30), london)
            .Should().Be(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc));
        FulfilmentPromiseCalculator.CutoffInstantUtc(new DateOnly(2026, 3, 29), new TimeOnly(1, 30, 30), london)
            .Should().Be(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc));

        // Autumn overlap 2026-10-25: 01:30 occurs twice — the FIRST occurrence is the BST one
        // (00:30 UTC). Once the cutoff has passed it stays passed: at 00:45 UTC (01:45 BST) the
        // book is closed, and the wall clock rolling back to 01:15 GMT (01:15 UTC) must not
        // reopen it — both instants are after the cutoff instant.
        var cutoff = FulfilmentPromiseCalculator.CutoffInstantUtc(new DateOnly(2026, 10, 25), new TimeOnly(1, 30), london);
        cutoff.Should().Be(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc));
        (new DateTime(2026, 10, 25, 0, 45, 0, DateTimeKind.Utc) > cutoff).Should().BeTrue();
        (new DateTime(2026, 10, 25, 1, 15, 0, DateTimeKind.Utc) > cutoff).Should().BeTrue("monotonic across the rollback");

        // And a plain BST-date daily flip: 10:00 vs 13:00 London either side of a 12:00 cutoff.
        var calendar = Launch(lead: 0, days: """["monday","tuesday","wednesday","thursday","friday","saturday","sunday"]""");
        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 21, 10))
            .Should().Be(new DateOnly(2026, 7, 21));
        FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 21, 13))
            .Should().Be(new DateOnly(2026, 7, 22));
    }

    [Fact]
    public void ExtremeStoredBlackouts_Should_DegradeToNoPromise_NotThrow()
    {
        // M1 — stored data is forever: a far-future blackout must clamp the horizon against
        // DateOnly.MaxValue and degrade, never throw a 500 into every later read.
        var calendar = Launch(blackouts: """["9999-12-31"]""");

        var act = () => FulfilmentPromiseCalculator.EarliestDelivery(calendar, Bst(2026, 7, 21, 10));

        act.Should().NotThrow();
    }

    // ─── Service (InMemory) ──────────────────────────────────────────────────

    private static (FulfilmentPromiseService Service, CommerceTestHarness.TestClock Clock, Aonik.Commerce.Persistence.CommerceDbContext Ctx) NewService()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var clock = new CommerceTestHarness.TestClock();   // 2026-06-18 12:00 UTC (a Thursday)
        return (new FulfilmentPromiseService(ctx, new Aonik.TestSupport.Multitenancy.TestTenantProvider(tenantId), clock), clock, ctx);
    }

    private static UpsertFulfilmentCalendarCommand Command(
        IReadOnlyList<string>? days = null,
        int lead = 0,
        IReadOnlyList<string>? blackouts = null,
        bool active = true,
        string timezone = "Europe/London",
        string? cutoffDay = null)
        => new(timezone, days ?? ["thursday"], new TimeOnly(12, 0), cutoffDay, lead, blackouts ?? [], active);

    [Fact]
    public async Task Service_Should_ReturnNull_WhenUnconfigured_AndEchoThePromiseOnUpsert()
    {
        var (service, _, _) = NewService();

        (await service.GetEarliestDeliveryAsync()).Should().BeNull("A4 — no calendar row");

        // Clock 2026-06-18 12:00 UTC = 13:00 London (BST) — past the 12:00 cutoff, so the
        // effective date is Friday 19 Jun and the first Thursday after is the 25th.
        var result = await service.UpsertCalendarAsync(Command());
        result.CurrentPromise.Should().NotBeNull("A5 — the operator sees the effect immediately");
        result.CurrentPromise!.EarliestDeliveryDate.Should().Be(new DateOnly(2026, 6, 25));

        var promise = await service.GetEarliestDeliveryAsync();
        promise!.EarliestDeliveryDate.Should().Be(new DateOnly(2026, 6, 25));
        promise.Timezone.Should().Be("Europe/London");
    }

    [Fact]
    public async Task Upsert_Should_EnforceTheValidations()
    {
        var (service, _, _) = NewService();

        var badTimezone = () => service.UpsertCalendarAsync(Command(timezone: "Mars/OlympusMons"));
        (await badTimezone.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("timezone");

        // O3 - the contract is IANA: a Windows id converts to its IANA equivalent, and a
        // free-text non-IANA value rejects even where the host OS could resolve it.
        var converted = await service.UpsertCalendarAsync(Command(timezone: "GMT Standard Time"));
        converted.Timezone.Should().Be("Europe/London");
        var notIana = () => service.UpsertCalendarAsync(Command(timezone: "Local"));
        await notIana.Should().ThrowAsync<StorefrontValidationException>();

        var badDay = () => service.UpsertCalendarAsync(Command(days: ["thorsday"]));
        (await badDay.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("weekday");

        var badCutoffDay = () => service.UpsertCalendarAsync(Command(cutoffDay: "someday"));
        await badCutoffDay.Should().ThrowAsync<StorefrontValidationException>();

        var badLead = () => service.UpsertCalendarAsync(Command(lead: 61));
        (await badLead.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("LeadDays");

        var emptyActive = () => service.UpsertCalendarAsync(Command(days: []));
        (await emptyActive.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("delivery day");

        // Inactive with no days is a legitimate parked state (the pack seed shape).
        var parked = await service.UpsertCalendarAsync(Command(days: [], active: false));
        parked.IsActive.Should().BeFalse();
        parked.CurrentPromise.Should().BeNull();

        var badDate = () => service.UpsertCalendarAsync(Command(blackouts: ["25/12/2026"]));
        (await badDate.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("ISO date");

        // M1 — a far-future blackout is a typo, not seasonal operational data.
        var farFuture = () => service.UpsertCalendarAsync(Command(blackouts: ["9999-12-31"]));
        (await farFuture.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("two years");
    }

    [Fact]
    public async Task Upsert_Should_PruneExpiredBlackouts_AndBoundTheFutureList()
    {
        var (service, _, _) = NewService();   // today (London) = 2026-06-18

        var result = await service.UpsertCalendarAsync(Command(
            blackouts: ["2026-06-17", "2026-06-18", "2026-06-25"]));
        result.BlackoutDates.Should().BeEquivalentTo(["2026-06-18", "2026-06-25"],
            "yesterday prunes; today and the future persist");
        result.CurrentPromise!.EarliestDeliveryDate.Should().Be(new DateOnly(2026, 7, 2),
            "the 25th is blacked out, so the promise skips a week");

        // A9 — 101 future dates reject naming the bound; 100 persist.
        var hundredOne = Enumerable.Range(0, 101)
            .Select(i => new DateOnly(2026, 7, 1).AddDays(i).ToString("yyyy-MM-dd"))
            .ToList();
        var tooMany = () => service.UpsertCalendarAsync(Command(blackouts: hundredOne));
        (await tooMany.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("100");

        var hundred = await service.UpsertCalendarAsync(Command(blackouts: hundredOne.Take(100).ToList()));
        hundred.BlackoutDates.Should().HaveCount(100);
    }

    [Fact]
    public void OfferedDates_Should_UseTheSameEarliestPromise_AndExcludeBlackoutsAndWrongWeekdays()
    {
        var calendar = Launch(days: """["thursday","saturday"]""", blackouts: """["2026-08-08"]""");
        var now = Bst(2026, 7, 21, 10);

        var result = FulfilmentPromiseCalculator.DeliveryDates(calendar, now, new DateOnly(2026, 8, 1), 15);

        result.Should().NotBeNull();
        result!.EarliestDeliveryDate.Should().Be(FulfilmentPromiseCalculator.EarliestDelivery(calendar, now)!.Value);
        result.Timezone.Should().Be("Europe/London");
        result.FromDate.Should().Be(new DateOnly(2026, 8, 1));
        result.ToDate.Should().Be(new DateOnly(2026, 8, 15));
        result.Dates.Should().Equal(new DateOnly(2026, 8, 6), new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 15));
    }

    [Theory]
    [InlineData(0, 30, true)]
    [InlineData(0, 45, false)]
    [InlineData(1, 15, false)]
    public void OfferedDates_Should_NotReopenTheDate_WhenAutumnClocksRollBack(int utcHour, int utcMinute, bool available)
    {
        var calendar = Launch(days: """["sunday"]""", lead: 0, cutoff: "01:30");
        var date = new DateOnly(2026, 10, 25);
        var now = new DateTime(2026, 10, 25, utcHour, utcMinute, 0, DateTimeKind.Utc);

        var result = FulfilmentPromiseCalculator.DeliveryDates(calendar, now, date, 1);

        result.Should().NotBeNull();
        result!.Dates.Contains(date).Should().Be(available);
    }

    [Fact]
    public void OfferedDates_Should_HandleTheLastRepresentableDate_WithoutOverflow()
    {
        var calendar = Launch(days: """["friday"]""", lead: 0, timezone: "UTC");
        var now = new DateTime(9999, 12, 31, 10, 0, 0, DateTimeKind.Utc);

        var result = FulfilmentPromiseCalculator.DeliveryDates(calendar, now, DateOnly.MaxValue, 1);

        result!.Dates.Should().Equal(DateOnly.MaxValue);
        calendar.BlackoutDatesJson = """["9999-12-31"]""";
        FulfilmentPromiseCalculator.DeliveryDates(calendar, now, DateOnly.MaxValue, 1).Should().BeNull();
        calendar.BlackoutDatesJson = "[]";
        calendar.LeadDays = 1;
        FulfilmentPromiseCalculator.DeliveryDates(calendar, now, DateOnly.MaxValue, 1).Should().BeNull();
    }

    [Fact]
    public async Task OfferedDates_Should_DefaultToEarliestAnd31Days_WithoutInventingABookingHorizon()
    {
        var (service, _, context) = NewService();
        using var _ = context;
        await service.UpsertCalendarAsync(Command());

        var result = await service.GetDeliveryDatesAsync();
        var future = await service.GetDeliveryDatesAsync(new DateOnly(2030, 1, 1), 62);
        var past = await service.GetDeliveryDatesAsync(new DateOnly(2026, 1, 1), 31);

        result!.FromDate.Should().Be(result.EarliestDeliveryDate);
        result.ToDate.Should().Be(result.FromDate.AddDays(30));
        result.Dates.Should().Equal(Enumerable.Range(0, 5).Select(week => new DateOnly(2026, 6, 25).AddDays(week * 7)));
        future!.Dates.Should().NotBeEmpty().And.OnlyContain(date => date.DayOfWeek == DayOfWeek.Thursday);
        future.ToDate.Should().Be(new DateOnly(2030, 3, 3));
        past!.Dates.Should().BeEmpty("a configured calendar can have no eligible dates in the requested range");
        context.ChangeTracker.HasChanges().Should().BeFalse("reading offered dates does not reserve or modify anything");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(int.MaxValue)]
    public async Task OfferedDates_Should_RejectUnboundedRanges_EvenWhenUnconfigured(int days)
    {
        var (service, _, context) = NewService();
        using var _ = context;

        var act = () => service.GetDeliveryDatesAsync(days: days);

        await act.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task OfferedDates_Should_RejectRangeOverflow()
    {
        var (service, _, context) = NewService();
        using var _ = context;

        var act = () => service.GetDeliveryDatesAsync(DateOnly.MaxValue, 2);

        await act.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Theory]
    [InlineData("2026-06-18")]
    [InlineData("2026-06-26")]
    [InlineData("2026-07-02")]
    public async Task SelectedDate_Should_RejectTooEarly_WrongWeekday_OrBlackout(string value)
    {
        var (service, _, context) = NewService();
        using var _ = context;
        await service.UpsertCalendarAsync(Command(blackouts: ["2026-07-02"]));

        var act = () => service.ValidateDeliveryDateAsync(DateOnly.Parse(value));

        await act.Should().ThrowAsync<StorefrontValidationException>();
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task SelectedDate_Should_RecheckClockAndCalendar_InsteadOfTrustingAnEarlierOffer()
    {
        var (service, clock, context) = NewService();
        using var _ = context;
        clock.UtcNow = Bst(2026, 6, 18, 12);
        await service.UpsertCalendarAsync(Command());
        var date = (await service.GetDeliveryDatesAsync())!.Dates.First();
        var selected = await service.ValidateDeliveryDateAsync(date);
        selected.Should().Be(new ValidatedDeliveryDateDto(new DateOnly(2026, 6, 18), "Europe/London"));

        clock.UtcNow = clock.UtcNow.AddTicks(1);
        var afterCutoff = () => service.ValidateDeliveryDateAsync(date);
        await afterCutoff.Should().ThrowAsync<StorefrontValidationException>();

        var nextDate = (await service.GetDeliveryDatesAsync())!.Dates.First();
        await service.UpsertCalendarAsync(Command(blackouts: [nextDate.ToString("yyyy-MM-dd")]));
        var afterBlackout = () => service.ValidateDeliveryDateAsync(nextDate);
        await afterBlackout.Should().ThrowAsync<StorefrontValidationException>();

        var anotherDate = (await service.GetDeliveryDatesAsync())!.Dates.First();
        await service.UpsertCalendarAsync(Command(active: false));
        (await service.GetDeliveryDatesAsync()).Should().BeNull();
        var afterDeactivation = () => service.ValidateDeliveryDateAsync(anotherDate);
        await afterDeactivation.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Theory]
    [InlineData("days-json")]
    [InlineData("days-null")]
    [InlineData("unknown-weekday")]
    [InlineData("null-weekday")]
    [InlineData("blackouts-json")]
    [InlineData("blackouts-null")]
    [InlineData("invalid-blackout")]
    [InlineData("null-blackout")]
    [InlineData("cutoff-day")]
    [InlineData("negative-lead")]
    [InlineData("excessive-lead")]
    [InlineData("timezone")]
    public async Task StoredInvalidCalendar_Should_WithholdBothPromiseAndDates_AndRejectSelection(string corruption)
    {
        var (service, _, context) = NewService();
        using var _ = context;
        await service.UpsertCalendarAsync(Command());
        var calendar = await context.FulfilmentCalendars.SingleAsync();
        switch (corruption)
        {
            case "days-json": calendar.DeliveryDaysJson = "{"; break;
            case "days-null": calendar.DeliveryDaysJson = "null"; break;
            case "unknown-weekday": calendar.DeliveryDaysJson = """["thursday","unknown"]"""; break;
            case "null-weekday": calendar.DeliveryDaysJson = """["thursday",null]"""; break;
            case "blackouts-json": calendar.BlackoutDatesJson = "{"; break;
            case "blackouts-null": calendar.BlackoutDatesJson = "null"; break;
            case "invalid-blackout": calendar.BlackoutDatesJson = """["2026-06-25","invalid"]"""; break;
            case "null-blackout": calendar.BlackoutDatesJson = "[null]"; break;
            case "cutoff-day": calendar.CutoffDayOfWeek = "invalid"; break;
            case "negative-lead": calendar.LeadDays = -1; break;
            case "excessive-lead": calendar.LeadDays = 61; break;
            case "timezone": calendar.Timezone = "Not/AZone"; break;
        }
        await context.SaveChangesAsync();

        (await service.GetEarliestDeliveryAsync()).Should().BeNull();
        (await service.GetDeliveryDatesAsync()).Should().BeNull();
        var select = () => service.ValidateDeliveryDateAsync(new DateOnly(2026, 6, 25));
        await select.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task OfferedDatesAndSelection_Should_RespectTenantAndSoftDeletion_WithoutFallback()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        using var context = CommerceTestHarness.CreateContext(options, tenantId);
        var clock = new CommerceTestHarness.TestClock();
        var service = new FulfilmentPromiseService(context, new Aonik.TestSupport.Multitenancy.TestTenantProvider(tenantId), clock);
        await service.UpsertCalendarAsync(Command());
        var date = (await service.GetDeliveryDatesAsync())!.Dates.First();
        var otherTenant = Guid.NewGuid();
        using var otherContext = CommerceTestHarness.CreateContext(options, otherTenant);
        var otherService = new FulfilmentPromiseService(otherContext, new Aonik.TestSupport.Multitenancy.TestTenantProvider(otherTenant), clock);

        (await otherService.GetDeliveryDatesAsync()).Should().BeNull();
        var otherSelection = () => otherService.ValidateDeliveryDateAsync(date);
        await otherSelection.Should().ThrowAsync<StorefrontValidationException>();

        var calendar = await context.FulfilmentCalendars.SingleAsync();
        calendar.IsDeleted = true;
        await context.SaveChangesAsync();
        (await service.GetDeliveryDatesAsync()).Should().BeNull();
        var deletedSelection = () => service.ValidateDeliveryDateAsync(date);
        await deletedSelection.Should().ThrowAsync<StorefrontValidationException>();
    }
}
