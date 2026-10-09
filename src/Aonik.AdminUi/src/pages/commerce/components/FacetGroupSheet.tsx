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
import type { FacetGroupDto } from '@/types/commerce';
import { facetSource, optionDraft, serializeFacetOptions } from '../lib/facetOptions';
import { merchandisingError, sortOrderFrom } from '../lib/merchandisingDraft';

export function FacetGroupSheet({ group, canWrite, requestConfig, isCurrent, onClose, onSaved }: {
  group?: FacetGroupDto; canWrite: boolean; requestConfig: AxiosRequestConfig; isCurrent: () => boolean;
  onClose: () => void; onSaved: (value: FacetGroupDto) => void;
}) {
  const [key, setKey] = useState(group?.key ?? '');
  const [label, setLabel] = useState(group?.label ?? '');
  const [kind, setKind] = useState(group?.matchKind ?? 'Tag');
  const [source, setSource] = useState(group?.sourcePath ?? '');
  const [sortOrder, setSortOrder] = useState(String(group?.sortOrder ?? 0));
  const [active, setActive] = useState(group?.isActive ?? true);
  const [rows, setRows] = useState(() => group ? group.options.map(optionDraft) : [optionDraft()]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const save = async () => {
    if (saving || !canWrite || !isCurrent()) return;
    setError(null);
    try {
      const normalizedKey = key.trim().toLowerCase();
      if (!group && !/^[a-z0-9-]{1,64}$/.test(normalizedKey)) throw new Error('Use 1 to 64 lowercase letters, digits or hyphens for the key.');
      if (!label.trim() || label.trim().length > 128) throw new Error('Enter a label of 1 to 128 characters.');
      const definition = { label: label.trim(), sourcePath: facetSource(kind, source),
        sortOrder: sortOrderFrom(sortOrder), optionsJson: serializeFacetOptions(rows, kind) };
      setSaving(true);
      const result = group
        ? await commerceStorefrontService.updateFacetGroup(group.id, { ...definition, isActive: active }, requestConfig)
        : await commerceStorefrontService.createFacetGroup({ ...definition, key: normalizedKey, matchKind: kind }, requestConfig);
      if (!isCurrent()) return;
      onSaved(result); toast.success('Facet group saved'); onClose();
    } catch (err: unknown) { if (isCurrent()) setError(merchandisingError(err, 'The facet group could not be saved.')); }
    finally { if (isCurrent()) setSaving(false); }
  };

  return <Sheet open onOpenChange={(open) => { if (!open && !saving) onClose(); }}><SheetContent size="lg">
    <SheetHeader title={group ? 'Edit facet group' : 'New facet group'} subtitle="The storefront submits option values, never labels." />
    <SheetBody>
      <fieldset disabled={saving || !canWrite} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1"><Label htmlFor="facet-key">Key</Label><Input id="facet-key" value={key} maxLength={64} readOnly={!!group} onChange={(event) => setKey(event.target.value)} /></div>
          <div className="space-y-1"><Label htmlFor="facet-kind">Match kind</Label><NativeSelect id="facet-kind" value={kind} disabled={!!group} onChange={(event) => setKind(event.target.value)}>
            {['Tag', 'Category', 'Attribute', 'Range'].map((value) => <option key={value}>{value}</option>)}
          </NativeSelect></div>
        </div>
        <p className="text-xs text-muted-foreground">A saved group's key and match kind never change — retire and replace instead.</p>
        <div className="space-y-1"><Label htmlFor="facet-label">Label</Label><Input id="facet-label" value={label} maxLength={128} onChange={(event) => setLabel(event.target.value)} /></div>
        {(kind === 'Attribute' || kind === 'Range') && <div className="space-y-1">
          <Label htmlFor="facet-source">Source path</Label><Input id="facet-source" value={source} maxLength={128} onChange={(event) => setSource(event.target.value)} />
          <p className="text-xs text-muted-foreground">Attribute paths start at the AttributesJson root, for example protein (not attributes.protein).
            Typed sources: nutrition.kcal, nutrition.proteinGrams and nutrition.fibreGrams use Range; lowSugar uses Attribute with true/false values.</p>
        </div>}
        <div className="space-y-1"><Label htmlFor="facet-sort">Sort order</Label><Input id="facet-sort" value={sortOrder} inputMode="numeric" onChange={(event) => setSortOrder(event.target.value)} /></div>
        {group && <div className="flex items-center gap-2"><Checkbox id="facet-active" checked={active} onCheckedChange={(value) => setActive(value === true)} /><Label htmlFor="facet-active">Active</Label></div>}
        <div className="space-y-3">
          <p className="text-sm font-medium">Options</p>
          {kind === 'Range' && <p className="text-xs text-muted-foreground">Order bands from lowest to highest. Minimum is inclusive; maximum is exclusive. Leave one end blank for an open range.</p>}
          {rows.map((row, index) => <div key={index} className="space-y-2 rounded-lg border border-border p-3">
            <div className="grid gap-2 sm:grid-cols-2">
              <div className="space-y-1"><Label htmlFor={`facet-value-${index}`}>Value {index + 1}</Label><Input id={`facet-value-${index}`} value={row.value} maxLength={256}
                onChange={(event) => setRows(rows.map((item, position) => position === index ? { ...item, value: event.target.value } : item))} /></div>
              <div className="space-y-1"><Label htmlFor={`facet-label-${index}`}>Label {index + 1}</Label><Input id={`facet-label-${index}`} value={row.label} maxLength={256}
                onChange={(event) => setRows(rows.map((item, position) => position === index ? { ...item, label: event.target.value } : item))} /></div>
              {kind === 'Range' && <>
                <div className="space-y-1"><Label htmlFor={`facet-min-${index}`}>Minimum (included)</Label><Input id={`facet-min-${index}`} value={row.min} inputMode="decimal"
                  onChange={(event) => setRows(rows.map((item, position) => position === index ? { ...item, min: event.target.value } : item))} /></div>
                <div className="space-y-1"><Label htmlFor={`facet-max-${index}`}>Maximum (excluded)</Label><Input id={`facet-max-${index}`} value={row.max} inputMode="decimal"
                  onChange={(event) => setRows(rows.map((item, position) => position === index ? { ...item, max: event.target.value } : item))} /></div>
              </>}
            </div>
            <Button variant="ghost" size="sm" onClick={() => setRows(rows.filter((_, position) => position !== index))}>Remove option {index + 1}</Button>
          </div>)}
          <Button variant="outline" size="sm" onClick={() => setRows([...rows, optionDraft()])}>Add option</Button>
        </div>
      </fieldset>
      {error && <Alert variant="destructive" className="mt-4"><AlertDescription>{error}</AlertDescription></Alert>}
    </SheetBody><SheetFooter>
      <Button variant="outline" disabled={saving} onClick={onClose}>Cancel</Button>
      <Button disabled={saving || !canWrite} onClick={() => void save()}>{saving ? 'Saving…' : 'Save facet group'}</Button>
    </SheetFooter>
  </SheetContent></Sheet>;
}
