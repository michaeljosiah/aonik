import { useEffect, useRef, useState } from 'react';
import { Card as AonikCard } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatCalendarDate } from '@/lib/format';
import { getSelectedTenant } from '@/lib/tenantContext';
import { commerceDeliveryCapacityService, type DeliveryAvailabilityDto, type DeliveryCapacityDto } from '@/services/commerceDeliveryCapacityService';
import { capacityRequest, deliveryError, monthDates, responseStatus } from './deliveryCalendarState';
import { DeliveryCapacityTable } from './DeliveryCapacityTable';

interface Props {
  tenantId: string;
  month: string;
  canWrite: boolean;
  calendarRevision: number;
  onSaved: () => void;
}

export function DeliveryCapacityEditor({ tenantId, month, canWrite, calendarRevision, onSaved }: Props) {
  const [rows, setRows] = useState<DeliveryCapacityDto[]>([]);
  const [availabilityResult, setAvailabilityResult] = useState<{ key: string; value: DeliveryAvailabilityDto | null; error: string | null } | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const [saving, setSaving] = useState(false);
  const [editor, setEditor] = useState<{ date: string; value: string; original?: DeliveryCapacityDto; conflict: boolean } | null>(null);
  const alive = useRef(false);
  const saveAbort = useRef<AbortController | null>(null);
  const dates = monthDates(month);
  const fromDate = dates[0];
  const days = dates.length;
  const availabilityKey = `${calendarRevision}:${revision}`;
  const availability = availabilityResult?.key === availabilityKey ? availabilityResult.value : null;
  const availabilityError = availabilityResult?.key === availabilityKey ? availabilityResult.error : null;

  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; saveAbort.current?.abort(); };
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    commerceDeliveryCapacityService.list(fromDate, days, { headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal })
      .then((result) => { if (!controller.signal.aborted) setRows(result); })
      .catch((err: unknown) => { if (!controller.signal.aborted) setLoadError(deliveryError(err, 'Capacity could not be loaded.')); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [tenantId, fromDate, days, revision]);

  useEffect(() => {
    const controller = new AbortController();
    commerceDeliveryCapacityService.availability(fromDate, days, { headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal })
      .then((result) => { if (!controller.signal.aborted) setAvailabilityResult({ key: availabilityKey, value: result, error: null }); })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setAvailabilityResult({ key: availabilityKey, value: null, error: responseStatus(err) === 404
          ? 'No delivery dates are currently offered by the saved calendar.' : 'Storefront availability could not be loaded.' });
      });
    return () => controller.abort();
  }, [tenantId, fromDate, days, availabilityKey]);

  function reload() {
    setRows([]); setLoading(true); setLoadError(null); setError(null); setEditor(null);
    setAvailabilityResult(null); setRevision((value) => value + 1);
  }

  async function save() {
    if (!editor || !canWrite || editor.conflict || saving || getSelectedTenant()?.tenantId !== tenantId) return;
    const controller = new AbortController();
    saveAbort.current = controller;
    setError(null);
    try {
      const request = capacityRequest(editor.value, editor.original);
      setSaving(true);
      const result = await commerceDeliveryCapacityService.update(editor.date, request,
        { headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal });
      if (!alive.current || controller.signal.aborted || getSelectedTenant()?.tenantId !== tenantId) return;
      setRows((current) => [...current.filter((row) => row.deliveryDate !== result.deliveryDate), result]);
      setEditor({ date: result.deliveryDate, value: String(result.capacity), original: result, conflict: false });
      setAvailabilityResult(null);
      onSaved();
    } catch (err) {
      if (!alive.current || controller.signal.aborted || getSelectedTenant()?.tenantId !== tenantId) return;
      if (responseStatus(err) === 409) {
        setEditor((current) => current && { ...current, conflict: true });
        setError('Capacity changed or the proposed budget is below current occupancy. Reload capacity before making another change. Your entered value is retained until reload.');
      } else setError(err instanceof Error ? err.message : deliveryError(err, 'Capacity could not be saved.'));
    } finally {
      if (alive.current && !controller.signal.aborted) setSaving(false);
    }
  }

  return <AonikCard title="Delivery capacity" subtitle="Explicit budgets in boxes. All box sizes and extras share the same daily budget."
    action={<Button variant="outline" size="sm" disabled={loading || saving} onClick={reload}>Reload capacity</Button>}>
    <p className="mb-3 text-sm text-muted-foreground">Unconfigured dates have unknown capacity. Held, payment-pending and committed boxes occupy the budget; this is not an order count.</p>
    {error && <Alert variant="destructive" className="mb-3"><AlertDescription>{error}</AlertDescription></Alert>}
    {loadError && <Alert variant="destructive" className="mb-3"><AlertDescription>{loadError}</AlertDescription></Alert>}
    {availabilityError && <p role="status" className="mb-3 text-sm text-muted-foreground">{availabilityError}</p>}
    {loading ? <p role="status">Loading capacity…</p> : !loadError ? <DeliveryCapacityTable dates={dates} rows={rows} availability={availability}
      canWrite={canWrite} saving={saving} onSelect={(date, row) => {
        setEditor({ date, value: row ? String(row.capacity) : '', original: row, conflict: false }); setError(null);
      }} /> : null}
    {editor && canWrite && <form className="mt-4 flex flex-wrap items-end gap-3" onSubmit={(event) => { event.preventDefault(); void save(); }}>
      <div className="space-y-2"><Label htmlFor="delivery-capacity">Box capacity for {formatCalendarDate(editor.date)}</Label>
        <Input id="delivery-capacity" type="number" min={0} max={2147483647} step={1} required disabled={saving}
          value={editor.value} onChange={(event) => setEditor({ ...editor, value: event.target.value })} /></div>
      <Button type="submit" disabled={saving || editor.conflict}>{saving ? 'Saving…' : editor.original ? 'Update capacity' : 'Configure capacity'}</Button>
      <p className="w-full text-xs text-muted-foreground">Zero explicitly closes the budget. Saving does not release reservations. A stale version requires reloading.</p>
    </form>}
  </AonikCard>;
}
