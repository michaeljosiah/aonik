import { Card as AonikCard } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { formatCurrency } from '@/lib/format';
import type { AdminCollectionDto, AdminCollectionItemDto, ExtrasListDto } from '@/types/commerce';

export function RailPreview({ collection, items, dirty, isExtras, extras, loading, error }: {
  collection: AdminCollectionDto; items: AdminCollectionItemDto[]; dirty: boolean; isExtras: boolean | null;
  extras: ExtrasListDto | null; loading: boolean; error: string | null;
}) {
  return <AonikCard title={isExtras ? 'Published extras rail' : dirty ? 'Unsaved membership preview' : 'Storefront rail preview'}
    subtitle={isExtras ? 'The current public response. Save membership changes to update this rail.'
      : dirty ? 'Local changes only. Save to publish this order.' : 'Active members in saved rank order.'}>
    {!collection.isActive ? <p className="text-sm text-muted-foreground">This collection is inactive — the public read serves nothing.</p>
      : isExtras === null ? <p role="status" className="text-sm text-muted-foreground">Storefront configuration is unavailable; the rail preview cannot be identified.</p>
      : isExtras ? <>
        {loading ? <p role="status" className="text-sm text-muted-foreground">Loading the public extras rail…</p>
          : error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>
          : extras && <>
            {extras.skipped > 0 && <Alert className="mb-3"><AlertDescription>
              The public read omitted and counted {extras.skipped} unpriceable {extras.skipped === 1 ? 'member' : 'members'}. They remain editable above.
            </AlertDescription></Alert>}
            <div className="flex gap-3 overflow-x-auto pb-2">
              {extras.rows.map((row) => <article key={row.productId} className="w-48 shrink-0 rounded-lg border border-border p-3">
                <p className="text-sm font-medium">{row.name}</p>
                <p className="mt-2 text-sm tabular-nums">{formatCurrency(row.unitPrice, row.currency)}</p>
                {row.unitSurcharge != null && row.unitSurcharge !== 0 && <p className="text-xs text-muted-foreground">
                  Surcharge: {formatCurrency(row.unitSurcharge, row.currency)}
                </p>}
              </article>)}
            </div>
            {extras.rows.length === 0 && <p className="text-sm text-muted-foreground">The public extras rail is empty.</p>}
          </>}
      </> : <>
        <div className="flex gap-3 overflow-x-auto pb-2">
          {items.filter((item) => item.status === 'Active').map((item) => <article key={item.productId} className="w-48 shrink-0 rounded-lg border border-border p-3">
            <p className="text-sm font-medium">{item.name}</p>
            <p className="mt-2 text-xs text-muted-foreground">Collection member</p>
          </article>)}
        </div>
        {!items.some((item) => item.status === 'Active') && <p className="text-sm text-muted-foreground">No active members to preview.</p>}
      </>}
  </AonikCard>;
}
