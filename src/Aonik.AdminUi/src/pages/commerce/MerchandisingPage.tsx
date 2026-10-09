import { useCallback, useEffect, useMemo, useState } from 'react';
import type { AxiosRequestConfig } from 'axios';
import { Plus, RefreshCw } from 'lucide-react';
import { Card as AonikCard, KpiTile, PageHeader, Pill } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { DataTable, type ColumnDef } from '@/components/ui/data-table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { getSelectedTenant } from '@/lib/tenantContext';
import { useModules } from '@/modules/useModules';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminCollectionDto, AdminCollectionSummaryDto, ExtrasListDto, FacetGroupDto } from '@/types/commerce';
import { CollectionEditorSheet } from './components/CollectionEditorSheet';
import { CollectionMembersEditor } from './components/CollectionMembersEditor';
import { FacetGroupSheet } from './components/FacetGroupSheet';
import { rangeLabel } from './lib/facetOptions';
import { merchandisingError } from './lib/merchandisingDraft';

interface TenantScope { tenantId: string | null; revision: number; controller: AbortController }

export function MerchandisingPage() {
  const [scope, setScope] = useState<TenantScope | null>(null);
  useEffect(() => {
    let controller: AbortController | undefined;
    let revision = 0;
    const reset = () => {
      // Abort synchronously: even requests awaiting the desktop adapter keep their original tenant.
      controller?.abort();
      controller = new AbortController();
      setScope({ tenantId: getSelectedTenant()?.tenantId ?? null, revision: ++revision, controller });
    };
    reset();
    window.addEventListener('aonik:tenant-changed', reset);
    return () => { controller?.abort(); window.removeEventListener('aonik:tenant-changed', reset); };
  }, []);
  if (!scope?.tenantId) return <div className="space-y-6 p-6 md:px-8">
    <PageHeader title="Merchandising" subtitle="Curated collections and storefront filter facets" />
    <p role="status" className="text-sm text-muted-foreground">{scope ? 'Select a tenant to manage merchandising.' : 'Loading tenant…'}</p>
  </div>;
  return <MerchandisingWorkspace key={`${scope.tenantId}-${scope.revision}`} tenantId={scope.tenantId} controller={scope.controller} />;
}

function MerchandisingWorkspace({ tenantId, controller }: { tenantId: string; controller: AbortController }) {
  const { allowsPolicy } = useModules();
  const canWrite = allowsPolicy('AdminWritePolicy');
  const requestConfig = useMemo<AxiosRequestConfig>(() => ({ headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal }), [tenantId, controller]);
  const isCurrent = useCallback(() => !controller.signal.aborted, [controller]);
  const [collections, setCollections] = useState<AdminCollectionSummaryDto[] | null>(null);
  const [facets, setFacets] = useState<FacetGroupDto[] | null>(null);
  const [extrasSlug, setExtrasSlug] = useState<string | null>(null);
  const [configReady, setConfigReady] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const [tab, setTab] = useState('collections');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<AdminCollectionDto | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailRevision, setDetailRevision] = useState(0);
  const [editorRevision, setEditorRevision] = useState(0);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [extras, setExtras] = useState<ExtrasListDto | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewRevision, setPreviewRevision] = useState(0);
  const [collectionEditor, setCollectionEditor] = useState<AdminCollectionDto | 'new' | null>(null);
  const [facetEditor, setFacetEditor] = useState<FacetGroupDto | 'new' | null>(null);

  useEffect(() => {
    let cancelled = false;
    void Promise.allSettled([
      commerceStorefrontService.listCollections(requestConfig),
      commerceStorefrontService.listFacetGroups(requestConfig),
      commerceStorefrontService.getPublicStorefrontConfig(requestConfig),
    ]).then(([collectionResult, facetResult, configResult]) => {
      if (cancelled || !isCurrent()) return;
      const failures: string[] = [];
      if (collectionResult.status === 'fulfilled') {
        setCollections(collectionResult.value);
        setSelectedId((previous) => collectionResult.value.some((item) => item.id === previous) ? previous : collectionResult.value[0]?.id ?? null);
      } else {
        setCollections(null); setSelectedId(null);
        failures.push(merchandisingError(collectionResult.reason, 'Collections could not be loaded.'));
      }
      if (facetResult.status === 'fulfilled') setFacets(facetResult.value);
      else { setFacets(null); failures.push(merchandisingError(facetResult.reason, 'Facet groups could not be loaded.')); }
      if (configResult.status === 'fulfilled') { setExtrasSlug(configResult.value.extrasCollectionSlug); setConfigReady(true); }
      else { setConfigReady(false); failures.push('Storefront configuration could not be loaded; extras pricing preview is unavailable.'); }
      setErrors(failures);
      setLoading(false);
    });
    return () => { cancelled = true; };
  }, [revision, requestConfig, isCurrent]);

  useEffect(() => {
    if (!selectedId) return;
    let cancelled = false;
    void commerceStorefrontService.getCollection(selectedId, requestConfig).then((value) => {
      if (!cancelled && isCurrent()) { setDetail(value); setDetailError(null); setEditorRevision((current) => current + 1); }
    }).catch((err: unknown) => {
      if (!cancelled && isCurrent()) setDetailError(merchandisingError(err, 'The collection could not be loaded.'));
    });
    return () => { cancelled = true; };
  }, [selectedId, detailRevision, requestConfig, isCurrent]);

  const selected = detail?.id === selectedId ? detail : null;
  const isExtras = configReady ? !!selected && selected.slug.toLowerCase() === extrasSlug?.toLowerCase() : null;
  useEffect(() => {
    if (!isExtras || !selected?.isActive) return;
    let cancelled = false;
    void commerceStorefrontService.getPublicExtras(requestConfig).then((value) => {
      if (!cancelled && isCurrent()) { setExtras(value); setPreviewError(null); }
    }).catch((err: unknown) => {
      if (!cancelled && isCurrent()) setPreviewError(merchandisingError(err, 'The public extras rail could not be loaded.'));
    });
    return () => { cancelled = true; };
  }, [isExtras, selected?.id, selected?.isActive, previewRevision, requestConfig, isCurrent]);

  const selectCollection = (id: string) => {
    if (dirty || saving || id === selectedId) return;
    setSelectedId(id); setDetail(null); setDetailError(null); setExtras(null); setPreviewError(null);
  };
  const savedCollection = (value: AdminCollectionDto) => {
    if (!isCurrent()) return;
    const { items, ...summary } = value;
    setCollections((previous) => [...(previous ?? []).filter((item) => item.id !== value.id), { ...summary, itemCount: items.length }]
      .sort((a, b) => a.sortOrder - b.sortOrder || a.slug.localeCompare(b.slug)));
    setSelectedId(value.id); setDetail(value); setDetailError(null); setDirty(false);
    setExtras(null); setPreviewError(null); setPreviewRevision((current) => current + 1);
  };
  const reload = () => {
    if (dirty || saving || loading) return;
    setLoading(true);
    setErrors([]); setDetail(null); setDetailError(null); setExtras(null); setPreviewError(null);
    setRevision((current) => current + 1); setDetailRevision((current) => current + 1); setPreviewRevision((current) => current + 1);
  };
  const facetColumns: ColumnDef<FacetGroupDto>[] = [
    { id: 'label', header: 'Facet', cell: (row) => <span className="font-medium">{row.label}<span className="block font-mono text-xs font-normal text-muted-foreground">{row.key}</span></span> },
    { id: 'kind', header: 'Match', cell: (row) => <Pill tone="info">{row.matchKind}</Pill> },
    { id: 'source', header: 'Source', cell: (row) => <span className="font-mono text-xs">{row.sourcePath ?? '—'}</span> },
    { id: 'options', header: 'Options', cell: (row) => <div className="flex max-w-md flex-wrap gap-1">{row.options.map((option) => <Pill key={option.value} tone="muted">
      {option.label} · {option.value}{row.matchKind === 'Range' ? ` ${rangeLabel(option.min, option.max)}` : ''}
    </Pill>)}</div> },
    { id: 'status', header: 'Status', cell: (row) => <Pill tone={row.isActive ? 'success' : 'muted'}>{row.isActive ? 'Active' : 'Inactive'}</Pill> },
  ];

  return <div className="space-y-6 p-6 md:px-8">
    <PageHeader title="Merchandising" subtitle="Curated collections and storefront filter facets"
      actions={<Button variant="outline" size="sm" disabled={dirty || saving || loading} onClick={reload}><RefreshCw aria-hidden />Reload</Button>} />
    {errors.map((error, index) => <Alert key={index} variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>)}
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <KpiTile label="Collections" value={collections == null ? '—' : String(collections.length)} />
      <KpiTile label="Saved memberships" value={collections == null ? '—' : String(collections.reduce((sum, item) => sum + item.itemCount, 0))} />
      <KpiTile label="Facet groups" value={facets == null ? '—' : String(facets.length)} />
      <KpiTile label="Draft products in selected collection" value={selected == null ? '—' : String(selected.items.filter((item) => item.status === 'Draft').length)} />
    </div>
    {!canWrite && <p className="text-sm text-muted-foreground">Read-only access. An administrator with write permission can change merchandising.</p>}
    <Tabs value={tab} onValueChange={(value) => { if (!dirty && !saving) setTab(value); }}>
      <TabsList variant="line"><TabsTrigger value="collections">Collections</TabsTrigger><TabsTrigger value="facets" disabled={dirty || saving}>Facet groups</TabsTrigger></TabsList>
      <TabsContent value="collections" className="mt-4">
        <div className="grid gap-5 xl:grid-cols-[18rem_minmax(0,1fr)]">
          <AonikCard title="Collections" action={canWrite && <Button size="sm" variant="outline" disabled={dirty || saving || loading} onClick={() => setCollectionEditor('new')}><Plus aria-hidden />New</Button>}>
            <div className="space-y-2">
              {collections?.map((collection) => <Button key={collection.id} variant={selectedId === collection.id ? 'secondary' : 'ghost'}
                className="h-auto w-full justify-start whitespace-normal py-3 text-left" disabled={dirty || saving || loading} onClick={() => selectCollection(collection.id)}>
                <span className="min-w-0"><span className="block font-medium">{collection.title}</span>
                  <span className="block font-mono text-xs text-muted-foreground">{collection.slug}</span>
                  <span className="mt-1 flex flex-wrap gap-1"><Pill tone={collection.isActive ? 'success' : 'muted'}>{collection.isActive ? 'Active' : 'Inactive'}</Pill><Pill tone="muted">{collection.itemCount} members</Pill>
                    {configReady && collection.slug.toLowerCase() === extrasSlug?.toLowerCase() && <Pill tone="info">Extras rail</Pill>}</span>
                </span>
              </Button>)}
              {collections?.length === 0 && <p className="text-sm text-muted-foreground">No collections yet.</p>}
              {collections == null && <p role="status" className="text-sm text-muted-foreground">{errors.length ? 'Collection list unavailable.' : 'Loading collections…'}</p>}
            </div>
          </AonikCard>
          <div className="min-w-0 space-y-4">
            {detailError && <Alert variant="destructive"><AlertDescription>{detailError}</AlertDescription></Alert>}
            {selected ? <>
              <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-lg font-semibold">{selected.title}</h2>
                {selected.subtitle && <p className="text-sm text-muted-foreground">{selected.subtitle}</p>}
                <p className="mt-1 text-xs text-muted-foreground">{selected.kind} · Sort order {selected.sortOrder}</p></div>
                {canWrite && <Button variant="outline" size="sm" disabled={dirty || saving || loading} onClick={() => setCollectionEditor(selected)}>Edit collection</Button>}
              </div>
              <CollectionMembersEditor key={`${selected.id}-${editorRevision}`} collection={selected} isExtras={isExtras} extras={extras}
                previewLoading={isExtras === true && extras == null && previewError == null} previewError={previewError} canWrite={canWrite && !loading}
                onSaved={savedCollection} onDirtyChange={setDirty} onSavingChange={setSaving} isCurrent={isCurrent} requestConfig={requestConfig} />
            </> : !detailError && <p role="status" className="text-sm text-muted-foreground">{selectedId ? 'Loading members…' : 'Choose a collection to view its members.'}</p>}
          </div>
        </div>
      </TabsContent>
      <TabsContent value="facets" className="mt-4 space-y-4">
        <AonikCard title="Facet groups" subtitle="Options use stable values; range bands include the minimum and exclude the maximum."
          action={canWrite && <Button size="sm" disabled={loading} onClick={() => setFacetEditor('new')}><Plus aria-hidden />New facet group</Button>}>
          <DataTable data={facets ?? []} columns={facetColumns} getRowId={(row) => row.id} showCheckboxes={false}
            loading={loading} emptyTitle={facets == null ? 'Facet groups unavailable' : 'No facet groups yet'}
            rowActions={canWrite ? (row) => <Button variant="ghost" size="sm" disabled={loading} onClick={() => setFacetEditor(row)}>Edit</Button> : undefined} />
        </AonikCard>
      </TabsContent>
    </Tabs>
    {collectionEditor && <CollectionEditorSheet key={collectionEditor === 'new' ? 'new' : collectionEditor.id}
      collection={collectionEditor === 'new' ? undefined : collectionEditor} canWrite={canWrite} isCurrent={isCurrent} requestConfig={requestConfig}
      onClose={() => setCollectionEditor(null)} onSaved={(value) => { savedCollection(value); setEditorRevision((current) => current + 1); }} />}
    {facetEditor && <FacetGroupSheet key={facetEditor === 'new' ? 'new' : facetEditor.id}
      group={facetEditor === 'new' ? undefined : facetEditor} canWrite={canWrite} isCurrent={isCurrent} requestConfig={requestConfig}
      onClose={() => setFacetEditor(null)} onSaved={(value) => {
        if (isCurrent()) setFacets((previous) => [...(previous ?? []).filter((item) => item.id !== value.id), value].sort((a, b) => a.sortOrder - b.sortOrder || a.key.localeCompare(b.key)));
      }} />}
  </div>;
}
