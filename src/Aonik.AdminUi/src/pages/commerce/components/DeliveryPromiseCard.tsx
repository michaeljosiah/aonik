import { Card as AonikCard } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { formatCalendarDate } from '@/lib/format';
import type { FulfilmentPromiseDto } from '@/types/commerce';

export function DeliveryPromiseCard({ promise, dirty, loading, error }: {
  promise: FulfilmentPromiseDto | null; dirty: boolean; loading: boolean; error: string | null;
}) {
  return <AonikCard title="Storefront delivery promise">
    {dirty && <Alert className="mb-3 border-warning/30 bg-warning/10 text-foreground"><AlertDescription>Unsaved changes — promise recomputes on save.</AlertDescription></Alert>}
    {loading ? <p role="status">Checking the saved promise…</p> : error ? <p role="alert" className="text-destructive">{error}</p>
      : promise ? <p className="text-sm">{formatCalendarDate(promise.earliestDeliveryDate)} · {promise.timezone}<span className="ml-2 text-muted-foreground">From the saved calendar and capacity.</span></p>
        : <p className="text-sm text-muted-foreground">No delivery promise is currently available. Check the saved calendar and its daily capacity; missing capacity and fully booked dates can prevent a promise.</p>}
  </AonikCard>;
}
