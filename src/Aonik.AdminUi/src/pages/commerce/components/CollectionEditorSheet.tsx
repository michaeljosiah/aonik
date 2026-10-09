import { useState } from 'react';
import type { AxiosRequestConfig } from 'axios';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminCollectionDto } from '@/types/commerce';
import { merchandisingError, sortOrderFrom } from '../lib/merchandisingDraft';

export function CollectionEditorSheet({ collection, canWrite, requestConfig, isCurrent, onClose, onSaved }: {
  collection?: AdminCollectionDto; canWrite: boolean; requestConfig: AxiosRequestConfig; isCurrent: () => boolean;
  onClose: () => void; onSaved: (value: AdminCollectionDto) => void;
}) {
  const [slug, setSlug] = useState(collection?.slug ?? '');
  const [title, setTitle] = useState(collection?.title ?? '');
  const [subtitle, setSubtitle] = useState(collection?.subtitle ?? '');
  const [kind, setKind] = useState(collection?.kind ?? 'Curated');
  const [sortOrder, setSortOrder] = useState(String(collection?.sortOrder ?? 0));
  const [active, setActive] = useState(collection?.isActive ?? true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const save = async () => {
    if (saving || !canWrite || !isCurrent()) return;
    setError(null);
    try {
      if (!title.trim() || title.trim().length > 128) throw new Error('Enter a title of 1 to 128 characters.');
      const normalizedSlug = slug.trim().toLowerCase();
      if (!collection && !/^[a-z0-9-]{1,64}$/.test(normalizedSlug)) throw new Error('Use 1 to 64 lowercase letters, digits or hyphens for the slug.');
      const order = sortOrderFrom(sortOrder);
      setSaving(true);
      const shared = { title: title.trim(), kind, sortOrder: order };
      const result = collection
        ? await commerceStorefrontService.updateCollection(collection.id,
          { ...shared, subtitle: subtitle.trim() || null, clearSubtitle: !subtitle.trim(), isActive: active }, requestConfig)
        : await commerceStorefrontService.createCollection({ ...shared, slug: normalizedSlug, subtitle: subtitle.trim() || null }, requestConfig);
      if (!isCurrent()) return;
      onSaved(result); toast.success('Collection saved'); onClose();
    } catch (err: unknown) { if (isCurrent()) setError(merchandisingError(err, 'The collection could not be saved.')); }
    finally { if (isCurrent()) setSaving(false); }
  };

  return <Sheet open onOpenChange={(open) => { if (!open && !saving) onClose(); }}><SheetContent size="md">
    <SheetHeader title={collection ? 'Edit collection' : 'New collection'} subtitle="The slug identifies this collection in storefront links and settings." />
    <SheetBody>
      <fieldset disabled={saving || !canWrite} className="space-y-4">
        <div className="space-y-1"><Label htmlFor="collection-slug">Slug</Label><Input id="collection-slug" value={slug} maxLength={64} readOnly={!!collection} onChange={(event) => setSlug(event.target.value)} />
          {collection && <p className="text-xs text-muted-foreground">The saved slug cannot change.</p>}</div>
        <div className="space-y-1"><Label htmlFor="collection-title">Title</Label><Input id="collection-title" value={title} maxLength={128} onChange={(event) => setTitle(event.target.value)} /></div>
        <div className="space-y-1"><Label htmlFor="collection-subtitle">Subtitle (optional)</Label><Input id="collection-subtitle" value={subtitle} maxLength={256} onChange={(event) => setSubtitle(event.target.value)} /></div>
        <div className="space-y-1"><Label htmlFor="collection-kind">Kind</Label><NativeSelect id="collection-kind" value={kind} onChange={(event) => setKind(event.target.value)}>
          {['Featured', 'Curated', 'Custom'].map((value) => <option key={value}>{value}</option>)}
        </NativeSelect></div>
        <div className="space-y-1"><Label htmlFor="collection-sort">Sort order</Label><Input id="collection-sort" inputMode="numeric" value={sortOrder} onChange={(event) => setSortOrder(event.target.value)} /></div>
        {collection ? <div className="flex items-center gap-2"><Checkbox id="collection-active" checked={active} onCheckedChange={(value) => setActive(value === true)} /><Label htmlFor="collection-active">Active</Label></div>
          : <p className="text-xs text-muted-foreground">New collections start active and empty.</p>}
      </fieldset>
      {error && <Alert variant="destructive" className="mt-4"><AlertDescription>{error}</AlertDescription></Alert>}
    </SheetBody><SheetFooter>
      <Button variant="outline" disabled={saving} onClick={onClose}>Cancel</Button>
      <Button disabled={saving || !canWrite} onClick={() => void save()}>{saving ? 'Saving…' : 'Save collection'}</Button>
    </SheetFooter>
  </SheetContent></Sheet>;
}
