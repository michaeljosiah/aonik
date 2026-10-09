using System.Globalization;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Fulfilment;

/// <summary>Spec 069 — the per-tenant fulfilment calendar and its computed promise.</summary>
public interface IFulfilmentPromiseService
{
    /// <summary>Earliest delivery date for the current tenant, or null when no active,
    /// resolvable calendar exists. Unconfigured is a state, not an error — never guess.</summary>
    Task<FulfilmentPromiseDto?> GetEarliestDeliveryAsync(CancellationToken cancellationToken = default);

    Task<DeliveryDatesDto?> GetDeliveryDatesAsync(
        DateOnly? fromDate = null, int days = 31, CancellationToken cancellationToken = default);

    /// <summary>Validates current calendar eligibility, without reserving capacity.</summary>
    Task<ValidatedDeliveryDateDto> ValidateDeliveryDateAsync(
        DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>The calendar including inactive (admin read); null when none exists.</summary>
    Task<FulfilmentCalendarDto?> GetCalendarAsync(CancellationToken cancellationToken = default);

    Task<FulfilmentCalendarDto> UpsertCalendarAsync(UpsertFulfilmentCalendarCommand command, CancellationToken cancellationToken = default);
}

internal sealed class FulfilmentPromiseService : IFulfilmentPromiseService
{
    private const int MaxLeadDays = FulfilmentPromiseCalculator.MaxLeadDays;
    private const int MaxFutureBlackouts = 100;

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public FulfilmentPromiseService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<FulfilmentPromiseDto?> GetEarliestDeliveryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var calendar = await _dbContext.FulfilmentCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        if (calendar is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        if (FulfilmentPromiseCalculator.EarliestDelivery(calendar, now) is not { } earliest) return null;
        var days = Math.Min(FulfilmentPromiseCalculator.MaxOfferedDays, DateOnly.MaxValue.DayNumber - earliest.DayNumber + 1);
        var eligible = FulfilmentPromiseCalculator.DeliveryDates(calendar, now, earliest, days)!;
        var availability = await DeliveryReservationService.ReadAvailabilityAsync(_dbContext, tenantId, eligible.Dates, now, cancellationToken);
        foreach (var date in eligible.Dates)
        {
            // An unknown earlier pool prevents asserting that a later date is the next one.
            if (availability[date].Status == DeliveryAvailabilityStatuses.Unknown) return null;
            if (availability[date].Status == DeliveryAvailabilityStatuses.Available)
                return new FulfilmentPromiseDto(date, calendar.Timezone);
        }
        return null;
    }

    public async Task<FulfilmentCalendarDto?> GetCalendarAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var calendar = await _dbContext.FulfilmentCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        return calendar is null ? null : Map(calendar) with { CurrentPromise = await GetEarliestDeliveryAsync(cancellationToken) };
    }

    public async Task<DeliveryDatesDto?> GetDeliveryDatesAsync(
        DateOnly? fromDate = null, int days = 31, CancellationToken cancellationToken = default)
    {
        FulfilmentPromiseCalculator.ValidateRange(fromDate, days);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var calendar = await _dbContext.FulfilmentCalendars.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        var now = _clock.UtcNow;
        var eligible = calendar is null ? null : FulfilmentPromiseCalculator.DeliveryDates(calendar, now, fromDate, days);
        if (eligible is null) return null;
        var availability = await DeliveryReservationService.ReadAvailabilityAsync(_dbContext, tenantId, eligible.Dates, now, cancellationToken);
        var results = Enumerable.Range(0, days).Select(offset => eligible.FromDate.AddDays(offset))
            .Select(date => availability.GetValueOrDefault(date) ?? new DeliveryDateAvailabilityDto(date, DeliveryAvailabilityStatuses.NoDelivery)).ToList();
        return eligible with
        {
            EarliestDeliveryDate = (await GetEarliestDeliveryAsync(cancellationToken))?.EarliestDeliveryDate,
            Dates = results.Where(date => date.Status == DeliveryAvailabilityStatuses.Available).Select(date => date.DeliveryDate).ToList(),
            Availability = results,
            ServerNowUtc = now
        };
    }

    public async Task<ValidatedDeliveryDateDto> ValidateDeliveryDateAsync(
        DateOnly date, CancellationToken cancellationToken = default)
    {
        // A cart already owning the final slot must pass calendar validation. Capacity is
        // checked through its reservation, not by requiring an additional free slot here.
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var calendar = await _dbContext.FulfilmentCalendars.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        var offered = calendar is null ? null : FulfilmentPromiseCalculator.DeliveryDates(calendar, _clock.UtcNow, date, 1);
        if (offered is null || !offered.Dates.Contains(date))
            throw new StorefrontValidationException("The selected delivery date is unavailable. Refresh the offered dates and choose again.");
        return new ValidatedDeliveryDateDto(date, offered.Timezone);
    }

    public async Task<FulfilmentCalendarDto> UpsertCalendarAsync(UpsertFulfilmentCalendarCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Known IANA timezone — every downstream computation depends on it resolving.
        var timezoneId = command.Timezone?.Trim() ?? string.Empty;

        // O3 - the contract is IANA ids: browsers cannot use a platform-native id like
        // "Eastern Standard Time" even where the host OS resolves it. A recognisable Windows id
        // converts; anything else without an Area/Location shape (or bare "UTC") rejects.
        if (!timezoneId.Contains('/') && !string.Equals(timezoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timezoneId, out var ianaId))
            {
                timezoneId = ianaId;
            }
            else
            {
                throw new StorefrontValidationException("'" + timezoneId + "' is not a known IANA timezone id.");
            }
        }

        TimeZoneInfo timezone;
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or ArgumentException or InvalidTimeZoneException)
        {
            throw new StorefrontValidationException($"'{timezoneId}' is not a known IANA timezone id.");
        }

        var deliveryDays = new List<string>();
        foreach (var raw in command.DeliveryDays ?? [])
        {
            var day = FulfilmentPromiseCalculator.TryParseDay(raw)
                ?? throw new StorefrontValidationException($"'{raw}' is not a weekday name (lower-case English, e.g. \"thursday\").");
            var canonical = day.ToString().ToLowerInvariant();
            if (!deliveryDays.Contains(canonical))
            {
                deliveryDays.Add(canonical);
            }
        }

        string? cutoffDay = null;
        if (!string.IsNullOrWhiteSpace(command.CutoffDayOfWeek))
        {
            var day = FulfilmentPromiseCalculator.TryParseDay(command.CutoffDayOfWeek)
                ?? throw new StorefrontValidationException($"'{command.CutoffDayOfWeek}' is not a weekday name.");
            cutoffDay = day.ToString().ToLowerInvariant();
        }

        if (command.LeadDays is < 0 or > MaxLeadDays)
        {
            throw new StorefrontValidationException($"LeadDays must be between 0 and {MaxLeadDays}.");
        }

        if (command.IsActive && deliveryDays.Count == 0)
        {
            throw new StorefrontValidationException("An active calendar needs at least one delivery day.");
        }

        // Blackouts: parse strictly, prune expired (relative to the calendar's own timezone —
        // a date is expired once it is behind TODAY there), bound the future list so a request
        // that passes validation can never fail at persistence with truncation (A9).
        var todayLocal = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc), timezone));
        var blackouts = new SortedSet<DateOnly>();
        foreach (var raw in command.BlackoutDates ?? [])
        {
            if (!DateOnly.TryParseExact(raw?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                throw new StorefrontValidationException($"'{raw}' is not an ISO date (yyyy-MM-dd).");
            }
            if (date < todayLocal)
            {
                continue;   // expired — pruned on save
            }
            // Blackouts are seasonal operational data (§2); a far-future date is a typo that
            // would otherwise ride into the horizon arithmetic forever.
            if (date > todayLocal.AddYears(2))
            {
                throw new StorefrontValidationException(
                    $"Blackout '{raw}' is more than two years out; blackout dates are near-term operational data.");
            }
            blackouts.Add(date);
        }
        if (blackouts.Count > MaxFutureBlackouts)
        {
            throw new StorefrontValidationException(
                $"At most {MaxFutureBlackouts} future blackout dates are supported; got {blackouts.Count}.");
        }

        var calendar = await _dbContext.FulfilmentCalendars
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        var creating = calendar is null;
        if (calendar is null)
        {
            calendar = new FulfilmentCalendar { Id = Guid.NewGuid(), TenantId = tenantId };
            _dbContext.FulfilmentCalendars.Add(calendar);
        }

        void Apply(FulfilmentCalendar target)
        {
            target.Timezone = timezoneId;
            target.DeliveryDaysJson = JsonSerializer.Serialize(deliveryDays);
            target.CutoffLocalTime = command.CutoffLocalTime;
            target.CutoffDayOfWeek = cutoffDay;
            target.LeadDays = command.LeadDays;
            target.BlackoutDatesJson = JsonSerializer.Serialize(blackouts.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList());
            target.IsActive = command.IsActive;
        }

        Apply(calendar);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (creating)
        {
            // Two first-time upserts raced the filtered unique (TenantId) index — this is an
            // UPSERT, so the loser adopts the winner's row and applies its own values rather
            // than surfacing a 500 (the 068 first-insert-race pattern).
            _dbContext.Entry(calendar).State = EntityState.Detached;
            calendar = await _dbContext.FulfilmentCalendars
                .FirstAsync(c => c.TenantId == tenantId, cancellationToken);
            Apply(calendar);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(calendar) with { CurrentPromise = await GetEarliestDeliveryAsync(cancellationToken) };
    }

    /// <summary>The DTO echoes the promise this calendar computes right now (A5).</summary>
    private FulfilmentCalendarDto Map(FulfilmentCalendar calendar)
    {
        var earliest = FulfilmentPromiseCalculator.EarliestDelivery(calendar, _clock.UtcNow);
        return new FulfilmentCalendarDto(
            calendar.Timezone,
            FulfilmentPromiseCalculator.ParseDayNames(calendar.DeliveryDaysJson),
            calendar.CutoffLocalTime,
            calendar.CutoffDayOfWeek,
            calendar.LeadDays,
            FulfilmentPromiseCalculator.ParseDates(calendar.BlackoutDatesJson)
                .OrderBy(d => d)
                .Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToList(),
            calendar.IsActive,
            earliest is { } date ? new FulfilmentPromiseDto(date, calendar.Timezone) : null);
    }
}
