import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { Card as AonikCard, PageHeader } from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Textarea } from '@/components/ui/textarea';
import { getSelectedTenant } from '@/lib/tenantContext';
import { useModules } from '@/modules/useModules';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminCollectionSummaryDto, ProductSummaryDto, StorefrontConfigDto } from '@/types/commerce';
import { StorefrontPreview } from './components/StorefrontPreview';
import { buildConfigCommand, configDraftFrom, validateConfigDraft, type ConfigDraft, type ConfigField, type ConfigTouched } from './lib/configForm';

export function StorefrontConfigPage() {
  const { allowsPolicy } = useModules();
  const canWrite = allowsPolicy('AdminWritePolicy');
  const epoch = useRef(0);
  const lifetime = useRef(new AbortController());
  const [tenantId, setTenantId] = useState(() => getSelectedTenant()?.tenantId ?? null);
  const [revision, setRevision] = useState(0);
  const [saved, setSaved] = useState<StorefrontConfigDto | null>(null);
  const [draft, setDraft] = useState<ConfigDraft | null>(null);
  const [touched, setTouched] = useState<ConfigTouched>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [collections, setCollections] = useState<AdminCollectionSummaryDto[]>([]);
  const [collectionsError, setCollectionsError] = useState<string | null>(null);
  const [bundles, setBundles] = useState<ProductSummaryDto[]>([]);
  const [bundlePage, setBundlePage] = useState(1);
  const [bundlePages, setBundlePages] = useState(1);
  const [bundleSearch, setBundleSearch] = useState('');
  const [bundlesLoading, setBundlesLoading] = useState(true);
  const [bundlesError, setBundlesError] = useState<string | null>(null);
  const [choicesRevision, setChoicesRevision] = useState(0);

  useEffect(() => {
    // StrictMode mounts effects twice, so replace a controller aborted by the first cleanup.
    if (lifetime.current.signal.aborted) lifetime.current = new AbortController();
    const invalidate = () => { epoch.current++; lifetime.current.abort(); };
    const reset = () => {
      invalidate(); lifetime.current = new AbortController();
      setTenantId(getSelectedTenant()?.tenantId ?? null);
      setSaved(null); setDraft(null); setTouched({}); setError(null); setPreviewError(null);
      setCollections([]); setBundles([]); setBundlePage(1); setBundlePages(1); setBundleSearch('');
      setLoading(true); setSaving(false); setRevision((value) => value + 1);
    };
    window.addEventListener('aonik:tenant-changed', reset);
    return () => { invalidate(); window.removeEventListener('aonik:tenant-changed', reset); };
  }, []);

  useEffect(() => {
    let cancelled = false;
    const requestEpoch = epoch.current;
    setLoading(true); setError(null); setPreviewError(null);
    if (!tenantId) { setLoading(false); setError('Select an organisation to load its storefront settings.'); return; }
    const request = { headers: { 'X-Tenant-Id': tenantId }, signal: lifetime.current.signal };
    commerceStorefrontService.getPublicStorefrontConfig(request).then((config) => {
      if (cancelled || epoch.current !== requestEpoch) return;
      setSaved(config); setDraft(configDraftFrom(config)); setTouched({}); setLoading(false);
    }).catch((reason: unknown) => {
      if (cancelled || epoch.current !== requestEpoch) return;
      setSaved(null); setDraft(null); setError(message(reason, 'Storefront configuration could not be loaded.')); setLoading(false);
    });
    return () => { cancelled = true; };
  }, [revision, tenantId]);

  useEffect(() => {
    let cancelled = false;
    const requestEpoch = epoch.current;
    setCollectionsError(null);
    if (!tenantId) return;
    commerceStorefrontService.listCollections({ headers: { 'X-Tenant-Id': tenantId }, signal: lifetime.current.signal }).then((items) => {
      if (!cancelled && requestEpoch === epoch.current) setCollections(items);
    }).catch((reason: unknown) => {
      if (!cancelled && requestEpoch === epoch.current) {
        setCollections([]); setCollectionsError(message(reason, 'Collections could not be loaded. The saved selection is unchanged.'));
      }
    });
    return () => { cancelled = true; };
  }, [revision, choicesRevision, tenantId]);

  useEffect(() => {
    let cancelled = false;
    const requestEpoch = epoch.current;
    setBundlesLoading(true); setBundlesError(null);
    if (!tenantId) return;
    const request = { headers: { 'X-Tenant-Id': tenantId }, signal: lifetime.current.signal };
    const timer = setTimeout(() => {
      commerceCatalogService.listProducts({ kind: 'Bundle', page: bundlePage, pageSize: 25, search: bundleSearch.trim() || undefined }, request)
        .then((result) => {
          if (cancelled || requestEpoch !== epoch.current) return;
          const pages = Math.max(1, result.totalPages);
          setBundlePages(pages);
          if (bundlePage > pages) { setBundlePage(pages); return; }
          setBundles(result.items); setBundlesLoading(false);
        }).catch((reason: unknown) => {
          if (cancelled || requestEpoch !== epoch.current) return;
          setBundles([]); setBundlesError(message(reason, 'Boxes could not be loaded. The saved selection is unchanged.')); setBundlesLoading(false);
        });
    }, 200);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [revision, choicesRevision, bundlePage, bundleSearch, tenantId]);

  const dirty = Object.keys(touched).length > 0;
  const invalid = draft ? validateConfigDraft(touched, draft) : null;
  const disabled = !canWrite || saving;
  const change = (field: ConfigField, value: string) => {
    setDraft((current) => current ? { ...current, [field]: value } : current);
    setTouched((current) => ({ ...current, [field]: true })); setError(null);
  };
  const reload = () => { epoch.current++; setLoading(true); setRevision((value) => value + 1); };

  async function save() {
    if (!tenantId || !draft || !dirty || invalid || disabled) return;
    const requestEpoch = epoch.current;
    const request = { headers: { 'X-Tenant-Id': tenantId }, signal: lifetime.current.signal };
    setSaving(true); setError(null); setPreviewError(null);
    try {
      const config = await commerceStorefrontService.updateStorefrontConfig(buildConfigCommand(touched, draft), request);
      if (epoch.current !== requestEpoch) return;
      setSaved(config); setDraft(configDraftFrom(config)); setTouched({});
      toast.success('Storefront settings saved');
      try {
        const fresh = await commerceStorefrontService.getPublicStorefrontConfig(request);
        if (epoch.current !== requestEpoch) return;
        setSaved(fresh); setDraft(configDraftFrom(fresh));
      } catch {
        if (epoch.current === requestEpoch) setPreviewError('Settings were saved, but the public preview could not be refreshed. The saved response is shown. Reload to check the public document.');
      }
    } catch (reason: unknown) {
      if (epoch.current === requestEpoch) setError(`${message(reason, 'Settings could not be saved.')} Your draft is retained. Some fields may already have saved; reload to check the stored values before retrying.`);
    } finally { if (epoch.current === requestEpoch) setSaving(false); }
  }

  if (loading) return <PageLoadingScreen message="Loading storefront configuration" />;
  return <div className="flex flex-col gap-5 p-6 md:px-8">
    <PageHeader title="Storefront config" subtitle="Edit storefront labels, delivery amounts and default selections"
      actions={<div className="flex gap-2"><Button variant="outline" disabled={saving} onClick={reload}>{dirty ? 'Discard and reload' : 'Reload'}</Button>
        <Button disabled={!draft || disabled || !dirty || !!invalid} onClick={() => void save()}>{saving ? 'Saving…' : 'Save settings'}</Button></div>} />
    {!canWrite && <p className="text-sm text-muted-foreground">Read-only access. Saving storefront settings requires write permission.</p>}
    {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
    {previewError && <Alert><AlertDescription>{previewError}</AlertDescription></Alert>}
    {saved && draft && <div className="grid items-start gap-5 xl:grid-cols-2">
      <AonikCard title="Storefront settings" subtitle={dirty ? 'Changes are not saved yet.' : 'Showing saved values.'}>
        <div className="space-y-5">
          <div className="space-y-1"><Label htmlFor="config-currency">Currency</Label><Input id="config-currency" value={saved.currency} readOnly />
            <p className="text-xs text-muted-foreground">Uses the organisation’s default currency.</p></div>
          <div className="space-y-2"><Label htmlFor="config-label">Recommended-choice label</Label>
            <Input id="config-label" value={draft.recommendedChoiceLabel} disabled={disabled} maxLength={4000} onChange={(event) => change('recommendedChoiceLabel', event.target.value)} />
            <Button variant="link" size="sm" disabled={disabled} onClick={() => change('recommendedChoiceLabel', '')}>Clear label override</Button></div>
          <div className="space-y-2"><Label htmlFor="config-page-size">Results per page</Label>
            <Input id="config-page-size" inputMode="numeric" value={draft.resultsPageSize} disabled={disabled} onChange={(event) => change('resultsPageSize', event.target.value)} /></div>
          <div className="grid gap-4 sm:grid-cols-2">{(['deliveryListAmount', 'deliveryChargedAmount'] as const).map((field) => <div key={field} className="space-y-2">
            <Label htmlFor={`config-${field}`}>{field === 'deliveryListAmount' ? 'Delivery list amount' : 'Delivery charged amount'} ({saved.currency})</Label>
            <Input id={`config-${field}`} inputMode="decimal" value={draft[field]} disabled={disabled} onChange={(event) => change(field, event.target.value)} />
          </div>)}</div>
          <p className="text-xs text-muted-foreground">The charged amount affects checkout. Use 0 for free delivery. Amounts use at most two decimal places; blank does not reset a numeric setting.</p>
          <div className="space-y-2"><Label htmlFor="config-box">Default box</Label>
            <Input aria-label="Search boxes" placeholder="Search boxes" value={bundleSearch} maxLength={200} disabled={disabled}
              onChange={(event) => { setBundleSearch(event.target.value); setBundlePage(1); setBundlesLoading(true); }} />
            <NativeSelect id="config-box" value={draft.defaultBoxSlug} disabled={disabled || bundlesLoading || !!bundlesError}
              onChange={(event) => change('defaultBoxSlug', event.target.value)}>
              <option value="" disabled>No box selected</option>
              {draft.defaultBoxSlug && !bundles.some((item) => item.slug === draft.defaultBoxSlug) && <option value={draft.defaultBoxSlug}>{draft.defaultBoxSlug} (retained selection; not on this page)</option>}
              {bundles.map((item) => <option key={item.id} value={item.slug}>{item.name} · {item.slug}{item.status !== 'Active' ? ` (${item.status})` : ''}</option>)}
            </NativeSelect>
            <div className="flex flex-wrap items-center gap-2"><Button variant="outline" size="sm" disabled={disabled || bundlesLoading || bundlePage <= 1} onClick={() => setBundlePage((page) => page - 1)}>Previous boxes</Button>
              <span className="text-xs text-muted-foreground">{bundlesLoading ? 'Loading boxes…' : `Page ${bundlePage} of ${bundlePages}`}</span>
              <Button variant="outline" size="sm" disabled={disabled || bundlesLoading || !!bundlesError || bundlePage >= bundlePages} onClick={() => setBundlePage((page) => page + 1)}>Next boxes</Button>
              <Button variant="link" size="sm" disabled={disabled} onClick={() => change('defaultBoxSlug', '')}>Clear box override</Button></div>
            <p className="text-xs text-muted-foreground">A draft or unavailable box stays selected, but has no live size-step preview.</p>
          </div>
          <div className="space-y-2"><Label htmlFor="config-extras">Extras collection</Label>
            <NativeSelect id="config-extras" value={draft.extrasCollectionSlug} disabled={disabled || !!collectionsError}
              onChange={(event) => change('extrasCollectionSlug', event.target.value)}>
              {!draft.extrasCollectionSlug && <option value="" disabled>Select a collection</option>}
              {draft.extrasCollectionSlug && !collections.some((item) => item.slug === draft.extrasCollectionSlug) && <option value={draft.extrasCollectionSlug}>{draft.extrasCollectionSlug} (retained selection; not in loaded collections)</option>}
              {collections.map((item) => <option key={item.id} value={item.slug}>{item.title} · {item.slug}{item.isActive ? '' : ' (inactive)'}</option>)}
            </NativeSelect>
            <p className="text-xs text-muted-foreground">Choose a collection to replace this setting; it cannot be cleared.</p>
          </div>
          {(bundlesError || collectionsError) && <Alert variant="destructive"><AlertDescription>{bundlesError} {collectionsError}
            <Button variant="link" size="sm" disabled={saving} onClick={() => setChoicesRevision((value) => value + 1)}>Retry choices</Button></AlertDescription></Alert>}
          <div className="space-y-2"><Label htmlFor="config-trigger">Back-to-top trigger JSON</Label>
            <Textarea id="config-trigger" rows={5} value={draft.backToTopTriggerJson} maxLength={4000} disabled={disabled}
              onChange={(event) => change('backToTopTriggerJson', event.target.value)} />
            <p className="text-xs text-muted-foreground">Enter a JSON object. It is stored as text and served as an object, preserving its properties.</p>
            <Button variant="link" size="sm" disabled={disabled} onClick={() => change('backToTopTriggerJson', '')}>Clear trigger override</Button>
          </div>
          {invalid && <p role="alert" className="text-sm text-destructive">{invalid}</p>}
          <p className="text-xs text-muted-foreground">Only fields you edit are saved. Clearing a supported override restores its configured default.</p>
        </div>
      </AonikCard>
      <StorefrontPreview config={saved} pending={dirty} />
    </div>}
  </div>;
}

function message(error: unknown, fallback: string): string {
  return error && typeof error === 'object' && 'userMessage' in error && typeof error.userMessage === 'string'
    ? error.userMessage : fallback;
}
