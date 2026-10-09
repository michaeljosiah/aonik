import { describe, expect, it } from 'vitest';
import { availabilityLabel, blankDeliveryCalendar, calendarDraft, calendarGrid, capacityRequest, initialCalendarMonth, isCalendarDate, isCalendarDirty, monthDates } from './deliveryCalendarState';

describe('delivery calendar drafts', () => {
  it('treats reordered dates and weekdays, daily null and minute/second time forms as unchanged', () => {
    const saved = { ...blankDeliveryCalendar(), deliveryDays: ['Thursday', 'Monday'], blackoutDates: ['2026-12-25', '2026-12-24'], cutoffLocalTime: '12:30:00' };
    const edited = { ...saved, deliveryDays: ['Monday', 'Thursday'], blackoutDates: ['2026-12-24', '2026-12-25'], cutoffDayOfWeek: '', cutoffLocalTime: '12:30' };
    expect(isCalendarDirty(edited, saved)).toBe(false);
    expect(saved.deliveryDays).toEqual(['Thursday', 'Monday']);
  });

  it('detects changes to every saved field while ignoring the computed promise', () => {
    const saved = { ...blankDeliveryCalendar(), currentPromise: { earliestDeliveryDate: '2026-10-15', timezone: 'Europe/London' } };
    const changes = [{ timezone: 'Europe/London' }, { deliveryDays: ['Thursday'] }, { cutoffLocalTime: '09:30' },
      { cutoffDayOfWeek: 'Monday' }, { leadDays: 7 }, { blackoutDates: ['2026-12-25'] }, { isActive: true }];
    for (const change of changes) expect(isCalendarDirty({ ...saved, ...change }, saved)).toBe(true);
    expect(isCalendarDirty(calendarDraft(saved), saved)).toBe(false);
  });

  it('starts unconfigured calendars parked and preserves blackout dates until the server saves them', () => {
    expect(blankDeliveryCalendar().isActive).toBe(false);
    expect(blankDeliveryCalendar().deliveryDays).toEqual([]);
    expect(isCalendarDirty(blankDeliveryCalendar(), null)).toBe(false);
    expect(calendarDraft({ ...blankDeliveryCalendar(), blackoutDates: ['2020-01-01', '2020-01-01'] }).blackoutDates).toEqual(['2020-01-01']);
  });
});

describe('date-only calendar preview', () => {
  it('opens on the promised month even when the next delivery crosses into a later month', () => {
    expect(initialCalendarMonth('2026-11-05', '2026-10-30')).toBe('2026-11');
    expect(initialCalendarMonth(null, '2026-10-30')).toBe('2026-10');
  });

  it('builds leap months and weekdays without converting dates through the viewer timezone', () => {
    expect(monthDates('2028-02')).toHaveLength(29);
    expect(monthDates('2028-02').at(-1)).toBe('2028-02-29');
    const grid = calendarGrid('2026-10', { ...blankDeliveryCalendar(), deliveryDays: ['Thursday'] }, null, false);
    expect(grid.firstWeekday).toBe(3);
    expect(grid.days[0].kind).toBe('delivery');
    expect(grid.days[1].kind).toBe('plain');
    expect(grid.days[7].kind).toBe('delivery');
  });

  it('shows the saved promise only while pristine and gives blackouts precedence', () => {
    const draft = { ...blankDeliveryCalendar(), deliveryDays: ['Thursday'], blackoutDates: ['2026-10-08'] };
    expect(calendarGrid('2026-10', draft, '2026-10-15', false).days[14].kind).toBe('promise');
    expect(calendarGrid('2026-10', draft, '2026-10-15', true).days[14].kind).toBe('delivery');
    expect(calendarGrid('2026-10', draft, '2026-10-08', false).days[7].kind).toBe('blackout');
  });

  it('rejects impossible and timestamp-shaped dates instead of rolling them into another day', () => {
    expect(isCalendarDate('2026-02-29')).toBe(false);
    expect(isCalendarDate('2028-02-29')).toBe(true);
    expect(isCalendarDate('2026-10-09T00:00:00Z')).toBe(false);
    expect(monthDates('2026-13')).toEqual([]);
  });
});

describe('capacity inputs', () => {
  const existing = { deliveryDate: '2026-10-15', unit: 'box', capacity: 12, occupied: 4, version: 'native-version' };

  it('requires explicit capacity and distinguishes a new zero budget from an unconfigured row', () => {
    expect(() => capacityRequest('', undefined)).toThrow('whole number');
    expect(capacityRequest('0', undefined)).toEqual({ unit: 'box', capacity: 0, expectedVersion: null });
  });

  it('carries the observed native version and disallows budgets below observed occupancy', () => {
    expect(capacityRequest('4', existing)).toEqual({ unit: 'box', capacity: 4, expectedVersion: 'native-version' });
    expect(() => capacityRequest('3', existing)).toThrow('held or committed');
    expect(existing.capacity).toBe(12);
  });

  it.each(['-1', '1.2', '1e3', '2147483648', 'NaN'])('rejects invalid box count %s', (value) => {
    expect(() => capacityRequest(value, undefined)).toThrow('whole number');
  });

  it('never turns missing or unrecognised availability into available capacity', () => {
    expect(availabilityLabel()).toBe('Unknown');
    expect(availabilityLabel('future_status')).toBe('Unknown');
    expect(availabilityLabel('fully_booked')).toBe('Fully booked');
    expect(availabilityLabel('no_delivery')).toBe('No delivery');
  });
});
