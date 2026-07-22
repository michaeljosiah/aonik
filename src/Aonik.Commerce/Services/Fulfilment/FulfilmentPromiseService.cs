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

    /// <summary>The calendar including inactive (admin read); null when none exists.</summary>
    Task<FulfilmentCalendarDto?> GetCalendarAsync(CancellationToken cancellationToken = default);

    Task<FulfilmentCalendarDto> UpsertCalendarAsync(UpsertFulfilmentCalendarCommand command, CancellationToken cancellationToken = default);
}

internal sealed class FulfilmentPromiseService : IFulfilmentPromiseService
{
    private const int MaxLeadDays = 60;
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

        var earliest = FulfilmentPromiseCalculator.EarliestDelivery(calendar, _clock.UtcNow);
        return earliest is { } date ? new FulfilmentPromiseDto(date, calendar.Timezone) : null;
    }

    public async Task<FulfilmentCalendarDto?> GetCalendarAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var calendar = await _dbContext.FulfilmentCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        return calendar is null ? null : Map(calendar);
    }

    public async Task<FulfilmentCalendarDto> UpsertCalendarAsync(UpsertFulfilmentCalendarCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Known IANA timezone — every downstream computation depends on it resolving.
        var timezoneId = command.Timezone?.Trim() ?? string.Empty;
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
            if (!DateOnly.TryParseExact(raw?.Trim(), "yyyy-MM-dd", out var date))
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
            target.Timezone = timezone.Id;
            target.DeliveryDaysJson = JsonSerializer.Serialize(deliveryDays);
            target.CutoffLocalTime = command.CutoffLocalTime;
            target.CutoffDayOfWeek = cutoffDay;
            target.LeadDays = command.LeadDays;
            target.BlackoutDatesJson = JsonSerializer.Serialize(blackouts.Select(d => d.ToString("yyyy-MM-dd")).ToList());
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

        return Map(calendar);
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
                .Select(d => d.ToString("yyyy-MM-dd"))
                .ToList(),
            calendar.IsActive,
            earliest is { } date ? new FulfilmentPromiseDto(date, calendar.Timezone) : null);
    }
}
