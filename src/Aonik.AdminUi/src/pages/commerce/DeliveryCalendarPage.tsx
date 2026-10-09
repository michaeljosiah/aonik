import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { Card as AonikCard, KpiTile, PageHeader } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Switch } from '@/components/ui/switch';
import { formatCalendarDate } from '@/lib/format';
import { getSelectedTenant } from '@/lib/tenantContext';
import { useModules } from '@/modules/useModules';
import { commerceStorefrontService, type UpsertFulfilmentCalendarRequest } from '@/services/commerceStorefrontService';
import type { FulfilmentCalendarDto, FulfilmentPromiseDto } from '@/types/commerce';
import { DeliveryCapacityEditor } from './components/DeliveryCapacityEditor';
import { DeliveryPromiseCard } from './components/DeliveryPromiseCard';
import { MonthGrid } from './components/MonthGrid';
import { blankDeliveryCalendar, calendarDraft, calendarGrid, deliveryError, deliveryWeekdays, initialCalendarMonth, isCalendarDate, isCalendarDirty, responseStatus } from './components/deliveryCalendarState';

function subscribeToTenantChange(changed: () => void) {
  window.addEventListener('aonik:tenant-changed', changed);
  return () => window.removeEventListener('aonik:tenant-changed', changed);
}

export function DeliveryCalendarPage() {
  const modules = useModules();
  const tenantId = useSyncExternalStore(subscribeToTenantChange, () => getSelectedTenant()?.tenantId ?? null, () => null);
  return <div className="flex flex-col gap-5 p-6 md:px-8">
    <PageHeader title="Delivery" subtitle="Manage the calendar, saved delivery promise and daily box capacity" />
    {tenantId && modules.allowsPolicy('AdminUserPolicy') ? <DeliveryCalendarWorkspace
      key={`${modules.contextKey}:${tenantId}`} tenantId={tenantId}
      canReadCapacity={modules.allowsPolicy('AdminReadPolicy')} canWrite={modules.allowsPolicy('AdminWritePolicy')} />
      : <p role="status" className="text-sm text-muted-foreground">{modules.loading ? 'Loading permissions…' : 'Select a tenant with permission to read its delivery calendar.'}</p>}
  </div>;
}

function DeliveryCalendarWorkspace({ tenantId, canReadCapacity, canWrite }: { tenantId: string; canReadCapacity: boolean; canWrite: boolean }) {
  const [committed, setCommitted] = useState<FulfilmentCalendarDto | null>(null);
  const [draft, setDraft] = useState<UpsertFulfilmentCalendarRequest>(blankDeliveryCalendar);
  const [promise, setPromise] = useState<FulfilmentPromiseDto | null>(null);
  const [promiseError, setPromiseError] = useState<string | null>(null);
  const [promiseLoading, setPromiseLoading] = useState(true);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);
  const [revision, setRevision] = useState(0);
  const [promiseRevision, setPromiseRevision] = useState(0);
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7));
  const [blackout, setBlackout] = useState('');
  const alive = useRef(false);
  const saveAbort = useRef<AbortController | null>(null);
  const dirty = isCalendarDirty(draft, committed);

  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; saveAbort.current?.abort(); };
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    commerceStorefrontService.getFulfilmentCalendar({ headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal })
      .then((result) => {
        if (!controller.signal.aborted) {
          setCommitted(result); setDraft(calendarDraft(result));
          setMonth(initialCalendarMonth(result.currentPromise?.earliestDeliveryDate, new Date().toISOString().slice(0, 10)));
        }
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (responseStatus(err) === 404) { setCommitted(null); setDraft(blankDeliveryCalendar()); }
        else setLoadError(deliveryError(err, 'The delivery calendar could not be loaded.'));
      }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [tenantId, revision]);

  useEffect(() => {
    const controller = new AbortController();
    commerceStorefrontService.getPublicDelivery({ headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal })
      .then((result) => { if (!controller.signal.aborted) { setPromise(result); setPromiseError(null); } })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setPromise(null);
        setPromiseError(responseStatus(err) === 404 ? null : 'The saved delivery promise could not be checked.');
      }).finally(() => { if (!controller.signal.aborted) setPromiseLoading(false); });
    return () => controller.abort();
  }, [tenantId, revision, promiseRevision]);

  function refreshPromise() {
    setPromiseLoading(true);
    setPromiseRevision((value) => value + 1);
  }

  function change(update: Partial<UpsertFulfilmentCalendarRequest>) {
    setDraft((current) => ({ ...current, ...update })); setSaved(false);
  }

  function addBlackout() {
    if (!isCalendarDate(blackout)) { setError('Choose a valid blackout date.'); return; }
    change({ blackoutDates: [...new Set([...draft.blackoutDates, blackout])].sort() });
    setBlackout(''); setError(null);
  }

  async function save() {
    if (!canWrite || saving || loading || loadError || getSelectedTenant()?.tenantId !== tenantId) return;
    const controller = new AbortController();
    saveAbort.current = controller;
    setSaving(true); setError(null); setSaved(false);
    try {
      const result = await commerceStorefrontService.upsertFulfilmentCalendar(calendarDraft(draft),
        { headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal });
      if (!alive.current || controller.signal.aborted || getSelectedTenant()?.tenantId !== tenantId) return;
      setCommitted(result); setDraft(calendarDraft(result)); setPromise(result.currentPromise);
      setSaved(true); refreshPromise();
    } catch (err) {
      if (alive.current && !controller.signal.aborted && getSelectedTenant()?.tenantId === tenantId)
        setError(deliveryError(err, 'The calendar could not be saved. Your changes are retained.'));
    } finally {
      if (alive.current && !controller.signal.aborted) setSaving(false);
    }
  }

  if (loading) return <p role="status" className="text-sm text-muted-foreground">Loading delivery calendar…</p>;
  if (loadError) return <Alert variant="destructive"><AlertDescription>{loadError}
    <Button variant="link" onClick={() => { setLoading(true); setLoadError(null); setRevision((value) => value + 1); }}>Retry</Button>
  </AlertDescription></Alert>;

  const nextDate = !promiseError && !promiseLoading ? promise?.earliestDeliveryDate : null;
  const grid = calendarGrid(month, draft, nextDate ?? null, dirty);
  const nextBlackout = [...(committed?.blackoutDates ?? [])].sort().find((date) => date >= new Date().toISOString().slice(0, 10));

  return <>
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <KpiTile label="Next delivery" value={nextDate ? formatCalendarDate(nextDate) : '—'} delta={promiseError ? 'Unavailable' : promiseLoading ? 'Checking…' : 'Saved calendar'} deltaTone="neutral" />
      <KpiTile label="Cutoff (calendar local time)" value={committed?.cutoffLocalTime.slice(0, 5) ?? '—'} delta={committed?.cutoffDayOfWeek ?? 'Daily'} deltaTone="neutral" />
      <KpiTile label="Lead days" value={committed ? String(committed.leadDays) : '—'} />
      <KpiTile label="Blackout dates" value={String(committed?.blackoutDates.length ?? 0)} delta={nextBlackout ? `Next ${formatCalendarDate(nextBlackout)}` : 'None upcoming'} deltaTone="neutral" />
    </div>
    <DeliveryPromiseCard promise={promise} dirty={dirty} loading={promiseLoading} error={promiseError} />
    <div className="grid items-start gap-5 xl:grid-cols-2">
      <AonikCard title={committed ? 'Calendar settings' : 'Set up the delivery calendar'} subtitle="Changes are saved together. Timezone and cutoff use the calendar’s local time.">
        {error && <Alert variant="destructive" className="mb-4"><AlertDescription>{error}</AlertDescription></Alert>}
        {saved && <p role="status" className="mb-4 text-sm text-primary">Calendar saved. The storefront promise is being checked.</p>}
        <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); void save(); }}>
          <fieldset className="space-y-4" disabled={!canWrite || saving}>
            <div className="space-y-2"><Label htmlFor="delivery-timezone">Timezone</Label>
              <Input id="delivery-timezone" placeholder="Europe/London" value={draft.timezone} required maxLength={100}
                onChange={(event) => change({ timezone: event.target.value })} /></div>
            <fieldset><legend className="mb-2 text-sm font-medium">Delivery weekdays</legend>
              <div className="flex flex-wrap gap-2">{deliveryWeekdays.map((day) => <Button key={day} type="button" size="sm" aria-pressed={draft.deliveryDays.includes(day)}
                variant={draft.deliveryDays.includes(day) ? 'default' : 'outline'} onClick={() => change({ deliveryDays: draft.deliveryDays.includes(day)
                  ? draft.deliveryDays.filter((value) => value !== day) : [...draft.deliveryDays, day] })}>{day.slice(0, 3)}</Button>)}</div>
            </fieldset>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2"><Label htmlFor="delivery-cutoff">Cutoff time</Label><Input id="delivery-cutoff" type="time" step={1} required
                value={draft.cutoffLocalTime} onChange={(event) => change({ cutoffLocalTime: event.target.value })} /></div>
              <div className="space-y-2"><Label htmlFor="delivery-cutoff-day">Cutoff day</Label><NativeSelect id="delivery-cutoff-day" value={draft.cutoffDayOfWeek ?? ''}
                onChange={(event) => change({ cutoffDayOfWeek: event.target.value || null })}><option value="">Daily</option>
                {deliveryWeekdays.map((day) => <option key={day} value={day}>{day}</option>)}</NativeSelect></div>
            </div>
            <div className="space-y-2"><Label htmlFor="delivery-lead">Lead days</Label><Input id="delivery-lead" type="number" min={0} max={60} step={1} required
              value={draft.leadDays} onChange={(event) => change({ leadDays: Number(event.target.value) })} /></div>
            <div className="space-y-2"><Label htmlFor="delivery-blackout">Blackout dates</Label>
              <div className="flex gap-2"><Input id="delivery-blackout" type="date" value={blackout} onChange={(event) => setBlackout(event.target.value)} />
                <Button type="button" variant="outline" disabled={!blackout} onClick={addBlackout}>Add date</Button></div>
              <div className="flex flex-wrap gap-2">{draft.blackoutDates.map((date) => <Button key={date} type="button" variant="outline" size="sm" aria-label={`Remove blackout ${date}`}
                onClick={() => change({ blackoutDates: draft.blackoutDates.filter((value) => value !== date) })}>{formatCalendarDate(date)} ×</Button>)}</div>
              <p className="text-xs text-muted-foreground">Expired dates are pruned on save. Up to 100 future dates within two years; existing dates remain until you remove them.</p>
            </div>
            <div className="flex items-center gap-3"><Switch id="delivery-active" checked={draft.isActive} onCheckedChange={(value) => change({ isActive: value })} />
              <Label htmlFor="delivery-active">Active calendar</Label></div>
          </fieldset>
          {canWrite ? <div className="flex gap-2"><Button type="submit" disabled={saving || (!dirty && !!committed)}>{saving ? 'Saving…' : 'Save calendar'}</Button>
            <Button type="button" variant="outline" disabled={!dirty || saving} onClick={() => { setDraft(calendarDraft(committed ?? blankDeliveryCalendar())); setError(null); setBlackout(''); }}>Discard changes</Button></div>
            : <p className="text-sm text-muted-foreground">You have read access. Calendar and capacity changes require write permission.</p>}
        </form>
      </AonikCard>
      <AonikCard title="Calendar preview" subtitle="Shading follows your edited calendar. Daily availability below comes from the saved calendar and capacity.">
        <div className="mb-4 space-y-2"><Label htmlFor="delivery-month">Month</Label><Input id="delivery-month" type="month" value={month}
          onChange={(event) => { if (isCalendarDate(`${event.target.value}-01`)) setMonth(event.target.value); }} /></div>
        <MonthGrid {...grid} legend={[{ kind: 'delivery', label: 'Delivery weekday' }, { kind: 'blackout', label: 'Blackout' }, { kind: 'promise', label: 'Saved promise (when unchanged)' }]} />
      </AonikCard>
    </div>
    {canReadCapacity ? <DeliveryCapacityEditor key={`${tenantId}:${month}`} tenantId={tenantId} month={month} canWrite={canWrite} calendarRevision={promiseRevision} onSaved={refreshPromise} />
      : <p className="text-sm text-muted-foreground">Capacity budgets require staff read permission.</p>}
  </>;
}
