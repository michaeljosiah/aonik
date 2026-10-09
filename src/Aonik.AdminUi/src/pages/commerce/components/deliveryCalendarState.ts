import type { UpsertFulfilmentCalendarRequest } from '@/services/commerceStorefrontService';
import type { DeliveryCapacityDto, UpdateDeliveryCapacityRequest } from '@/services/commerceDeliveryCapacityService';
import type { MonthGridDay } from './monthGridMath';

export const deliveryWeekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

function weekdayName(value: string): string {
  const day = value.trim();
  return deliveryWeekdays.find((name) => name.toLowerCase() === day.toLowerCase()) ?? day;
}

export const blankDeliveryCalendar = (): UpsertFulfilmentCalendarRequest => ({
  timezone: '', deliveryDays: [], cutoffLocalTime: '12:00:00', cutoffDayOfWeek: null,
  leadDays: 0, blackoutDates: [], isActive: false,
});

export function calendarDraft(value: UpsertFulfilmentCalendarRequest): UpsertFulfilmentCalendarRequest {
  return { timezone: value.timezone.trim(), deliveryDays: [...new Set(value.deliveryDays.map(weekdayName))].sort(),
    cutoffLocalTime: value.cutoffLocalTime.length === 5 ? `${value.cutoffLocalTime}:00` : value.cutoffLocalTime,
    cutoffDayOfWeek: value.cutoffDayOfWeek?.trim() ? weekdayName(value.cutoffDayOfWeek) : null, leadDays: value.leadDays,
    blackoutDates: [...new Set(value.blackoutDates)].sort(), isActive: value.isActive };
}

export function isCalendarDirty(draft: UpsertFulfilmentCalendarRequest, committed: UpsertFulfilmentCalendarRequest | null): boolean {
  return JSON.stringify(calendarDraft(draft)) !== JSON.stringify(calendarDraft(committed ?? blankDeliveryCalendar()));
}

export function isCalendarDate(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(`${value}T00:00:00Z`))
    && new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value;
}

export function monthDates(month: string): string[] {
  const start = `${month}-01`;
  if (!isCalendarDate(start)) return [];
  const date = new Date(`${start}T00:00:00Z`);
  const dates: string[] = [];
  while (date.toISOString().startsWith(month)) {
    dates.push(date.toISOString().slice(0, 10));
    date.setUTCDate(date.getUTCDate() + 1);
  }
  return dates;
}

export function initialCalendarMonth(promiseDate: string | null | undefined, today: string): string {
  return (promiseDate && isCalendarDate(promiseDate) ? promiseDate : today).slice(0, 7);
}

export function calendarGrid(month: string, draft: UpsertFulfilmentCalendarRequest, promise: string | null, dirty: boolean) {
  const dates = monthDates(month);
  const first = new Date(`${dates[0]}T00:00:00Z`);
  return {
    monthLabel: first.toLocaleDateString('en-GB', { month: 'long', year: 'numeric', timeZone: 'UTC' }),
    firstWeekday: (first.getUTCDay() + 6) % 7,
    days: dates.map((date, index): MonthGridDay => ({ day: index + 1,
      kind: draft.blackoutDates.includes(date) ? 'blackout'
        : !dirty && promise === date ? 'promise'
          : draft.deliveryDays.includes(deliveryWeekdays[(first.getUTCDay() + index + 6) % 7]) ? 'delivery' : 'plain' })),
  };
}

export function capacityRequest(value: string, current: DeliveryCapacityDto | undefined): UpdateDeliveryCapacityRequest {
  if (!/^\d+$/.test(value) || !Number.isSafeInteger(Number(value)) || Number(value) > 2147483647)
    throw new Error('Enter a whole number of boxes, zero or more.');
  if (current && Number(value) < current.occupied)
    throw new Error('Capacity cannot be less than the boxes already held or committed.');
  return { unit: 'box', capacity: Number(value), expectedVersion: current?.version ?? null };
}

export function deliveryError(error: unknown, fallback: string): string {
  return error && typeof error === 'object' && 'userMessage' in error ? String(error.userMessage) : fallback;
}

export function responseStatus(error: unknown): number | undefined {
  return (error as { response?: { status?: number } } | null)?.response?.status;
}

export function availabilityLabel(status?: string): string {
  return ({ available: 'Available', fully_booked: 'Fully booked', no_delivery: 'No delivery', unknown: 'Unknown' } as Record<string, string>)[status ?? ''] ?? 'Unknown';
}
