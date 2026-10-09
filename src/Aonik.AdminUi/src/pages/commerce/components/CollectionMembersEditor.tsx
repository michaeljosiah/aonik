import { useEffect, useState } from 'react';
import type { AxiosRequestConfig } from 'axios';
import { ArrowDown, ArrowUp, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { formatCurrency } from '@/lib/format';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminCollectionDto, ExtrasListDto } from '@/types/commerce';
import { addMember, memberState, membershipLines, merchandisingError, moveMember, sameMembership } from '../lib/merchandisingDraft';
import { CollectionProductPicker } from './CollectionProductPicker';
import { RailPreview } from './RailPreview';

export function CollectionMembersEditor({ collection, isExtras, extras, previewLoading, previewError, canWrite,
  onSaved, onDirtyChange, onSavingChange, isCurrent, requestConfig }: {
  collection: AdminCollectionDto; isExtras: boolean | null; extras: ExtrasListDto | null; previewLoading: boolean; previewError: string | null;
  canWrite: boolean; onSaved: (value: AdminCollectionDto) => void; onDirtyChange: (dirty: boolean) => void;
  onSavingChange: (saving: boolean) => void; isCurrent: () => boolean; requestConfig: AxiosRequestConfig;
}) {
  const [items, setItems] = useState(() => [...collection.items].sort((a, b) => a.rank - b.rank));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [picking, setPicking] = useState(false);
  const dirty = !sameMembership(items, [...collection.items].sort((a, b) => a.rank - b.rank));
  useEffect(() => { onDirtyChange(dirty); }, [dirty, onDirtyChange]);

  const save = async () => {
    if (!canWrite || saving || !dirty || !isCurrent()) return;
    setSaving(true); onSavingChange(true); setError(null);
    try {
      const result = await commerceStorefrontService.replaceCollectionItems(collection.id, membershipLines(items), requestConfig);
      if (!isCurrent()) return;
      setItems([...result.items].sort((a, b) => a.rank - b.rank));
      onSaved(result); toast.success('Collection members saved');
    } catch (err: unknown) {
      if (isCurrent()) setError(merchandisingError(err, 'Members could not be saved. Your draft is unchanged.'));
    } finally { if (isCurrent()) { setSaving(false); onSavingChange(false); } }
  };

  return <div className="space-y-4">
    <AonikCard title="Ranked members" subtitle={dirty ? 'Unsaved changes — save or discard before changing collections.' : 'All staged members, including drafts.'}
      action={canWrite && <Button size="sm" variant="outline" disabled={saving} onClick={() => setPicking(true)}><Plus aria-hidden />Add member</Button>}>
      {error && <Alert variant="destructive" className="mb-3"><AlertDescription>{error}</AlertDescription></Alert>}
      <ol className="divide-y divide-border">
        {items.map((item, index) => <li key={item.productId} className={`flex flex-wrap items-center gap-3 py-3 ${item.status === 'Active' ? '' : 'bg-muted/30'}`}>
          <span className="w-6 font-mono text-sm tabular-nums text-muted-foreground">{String(index + 1).padStart(2, '0')}</span>
          <span className="min-w-0 flex-1 text-sm font-medium">{item.name}<span className="block font-mono text-xs font-normal text-muted-foreground">{item.slug}</span></span>
          {isExtras && <span className="text-sm tabular-nums">{item.isPriceable === true && item.unitPrice != null && item.currency ? formatCurrency(item.unitPrice, item.currency) : '—'}</span>}
          <Pill tone={item.status !== 'Active' || item.isPriceable === false ? 'warning' : !collection.isActive || isExtras === null || item.isPriceable === null && isExtras ? 'muted' : 'success'}>{memberState(item, isExtras, collection.isActive)}</Pill>
          {canWrite && <div className="flex items-center gap-1">
            <Button variant="ghost" size="icon" disabled={saving || index === 0} aria-label={`Move ${item.name} up`} onClick={() => setItems(moveMember(items, index, -1))}><ArrowUp aria-hidden /></Button>
            <Button variant="ghost" size="icon" disabled={saving || index === items.length - 1} aria-label={`Move ${item.name} down`} onClick={() => setItems(moveMember(items, index, 1))}><ArrowDown aria-hidden /></Button>
            <Button variant="ghost" size="sm" disabled={saving} onClick={() => setItems(items.filter((member) => member.productId !== item.productId))}>Remove</Button>
          </div>}
        </li>)}
      </ol>
      {!items.length && <p className="py-3 text-sm text-muted-foreground">No members. Saving an empty list clears this collection.</p>}
      {canWrite && <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" disabled={!dirty || saving} onClick={() => { setItems([...collection.items].sort((a, b) => a.rank - b.rank)); setError(null); }}>Discard</Button>
        <Button disabled={!dirty || saving} onClick={() => void save()}>{saving ? 'Saving…' : 'Save members'}</Button>
      </div>}
    </AonikCard>
    <RailPreview collection={collection} items={items} dirty={dirty} isExtras={isExtras} extras={extras} loading={previewLoading} error={previewError} />
    {picking && <CollectionProductPicker selectedIds={items.map((item) => item.productId)} canWrite={canWrite && !saving} isCurrent={isCurrent} requestConfig={requestConfig}
      onAdd={(product) => setItems((previous) => addMember(previous, product))} onClose={() => setPicking(false)} />}
  </div>;
}
