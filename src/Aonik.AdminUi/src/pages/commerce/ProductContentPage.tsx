// Product content (Spec 075) — declarations, the review queue, and coverage.
//
// Spec 067's safety model is the page's spine, and it is asymmetric on purpose:
//
//   FIGURES may fall back, captioned as the standard preparation.
//   DECLARATIONS are exact-authored or WITHHELD. Never substituted, never inherited.
//
// Everything here follows from that. The workbench renders what the CUSTOMER reads rather than
// what the row stores, so a block under review shows its declarations withheld even though the
// text is present. Staleness is server-computed and never re-derived here — the client cannot
// canonicalise a selection, and the direction a client re-implementation would drift is the
// dangerous one: labelling a withholding block "Authored".

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { Card as AonikCard, KpiTile, PageHeader, Pill, type PillTone } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceContentService } from '@/services/commerceContentService';
import type {
  AdminProductContentDto,
  ContentCoverageDto,
  ContentStatusRowDto,
  EffectiveOptionGroupDto,
  ProductContentVariantDto,
} from '@/types/commerce';

import { ContentBlockSheet } from './components/ContentBlockSheet';
import { ContentVariantSheet } from './components/ContentVariantSheet';
import { ContentWorkbench } from './components/ContentWorkbench';
import {
  countsAsPublished,
  deriveContentState,
  isPublishedDenominator,
  type ContentState,
} from './lib/contentState';

/** Status rows fetched for the rail, queue and KPIs. Named wherever a figure describes it. */
const STATUS_PAGE_SIZE = 200;

const STATE_TONE: Record<ContentState, PillTone> = {
  authored: 'success',
  review: 'warning',
  withheld: 'muted',
  none: 'default',
};

const STATE_LABEL: Record<ContentState, string> = {
  authored: 'Authored',
  review: 'Review',
  withheld: 'Withheld',
  none: 'Nothing published',
};

export function ProductContentPage() {
  const [rows, setRows] = useState<ContentStatusRowDto[]>([]);
  const [rowsComplete, setRowsComplete] = useState(true);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [content, setContent] = useState<AdminProductContentDto | null>(null);
  const [coverage, setCoverage] = useState<ContentCoverageDto | null>(null);
  const [groups, setGroups] = useState<EffectiveOptionGroupDto[]>([]);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [editingBlock, setEditingBlock] = useState(false);
  const [variantSheet, setVariantSheet] = useState<{
    variant: ProductContentVariantDto | null;
    selectionJson: string | null;
  } | null>(null);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const listRequestRef = useRef(0);
  const detailRequestRef = useRef(0);

  const loadRows = useCallback(async () => {
    const requestId = listRequestRef.current + 1;
    listRequestRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const page = await commerceContentService.listContentStatus(1, STATUS_PAGE_SIZE);
      if (listRequestRef.current !== requestId) return;
      setRows(page.items);
      setRowsComplete(page.items.length >= page.totalCount);
      setSelectedId((current) =>
        current && page.items.some((r) => r.productId === current)
          ? current
          : (page.items[0]?.productId ?? null),
      );
    } catch (err: unknown) {
      if (listRequestRef.current !== requestId) return;
      // The last good list is kept: an open editor reading an empty catalogue is how a
      // refresh failure turns into a destructive save (the Spec 074 lesson).
      setError(readMessage(err) || 'Content status could not be refreshed — showing the last data loaded.');
    } finally {
      if (listRequestRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadRows();
  }, [loadRows]);

  const loadDetail = useCallback(async (productId: string) => {
    const requestId = detailRequestRef.current + 1;
    detailRequestRef.current = requestId;
    setDetailLoading(true);
    setDetailError(null);
    try {
      // Coverage and the product's effective offer are fetched for the SELECTED product only —
      // per-product reads for the whole rail would be a fan-out on every page load.
      const [admin, cover, product] = await Promise.all([
        commerceContentService.getAdminContent(productId),
        commerceContentService.getCoverage(productId).catch(() => null),
        commerceCatalogService.getProduct(productId).catch(() => null),
      ]);
      if (detailRequestRef.current !== requestId) return;
      setContent(admin);
      setCoverage(cover);
      setGroups(product?.effectiveOptionGroups ?? []);
    } catch (err: unknown) {
      if (detailRequestRef.current !== requestId) return;
      setContent(null);
      setCoverage(null);
      setGroups([]);
      setDetailError(readMessage(err) || 'This product’s content could not be read.');
    } finally {
      if (detailRequestRef.current === requestId) setDetailLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedId) void loadDetail(selectedId);
  }, [selectedId, loadDetail]);

  const selectedRow = rows.find((r) => r.productId === selectedId) ?? null;

  // The rail's pill comes from the SAME mapper the workbench uses, so a product can never read
  // Authored in the list and withheld in the panel beside it.
  const stateOf = useCallback(
    (row: ContentStatusRowDto): ContentState =>
      deriveContentState(
        row.hasBlock ? { ingredients: row.hasDeclarations ? 'authored' : null, allergens: null } : null,
        row.isStale,
      ),
    [],
  );

  const selectedState = content
    ? deriveContentState(content.block, content.isStale)
    : selectedRow
      ? stateOf(selectedRow)
      : 'none';

  const kpis = useMemo(() => {
    const denominator = rows.filter(isPublishedDenominator).length;
    return {
      published: rows.filter(countsAsPublished).length,
      denominator,
      variants: rows.reduce((sum, r) => sum + r.variantCount, 0),
      awaitingReview: rows.filter((r) => r.isStale).length,
    };
  }, [rows]);

  const reviewQueue = useMemo(() => rows.filter((r) => r.isStale), [rows]);

  const confirmReview = async (productId: string) => {
    try {
      // No client-side staleness re-check here on purpose: ConfirmContentReviewAsync recomputes
      // the all-defaults binding INSIDE its serialized write attempt, so "still correct" means
      // current at commit rather than at request parse. A guard here would be theatre.
      await commerceContentService.confirmReview(productId);
      toast.success('Review confirmed — declarations serve again');
      await loadRows();
      if (productId === selectedId) await loadDetail(productId);
    } catch (err: unknown) {
      toast.error(readMessage(err) || 'The review could not be confirmed.');
    }
  };

  if (initialLoad) return <PageLoadingScreen message="Loading product content" />;

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Commerce"
        title="Product content"
        subtitle="Figures may fall back, captioned. Declarations are exact-authored or withheld — never substituted."
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile
          label="Publishing figures"
          value={`${kpis.published} / ${kpis.denominator}`}
          delta={rowsComplete ? 'active products' : `first ${rows.length} scanned`}
          deltaTone="neutral"
        />
        <KpiTile
          label="Combination variants"
          value={kpis.variants.toLocaleString()}
          delta={rowsComplete ? 'all products' : `first ${rows.length} scanned`}
          deltaTone="neutral"
        />
        <KpiTile
          label="Coverage gaps"
          value={coverage ? coverage.singleChoiceGaps.length.toLocaleString() : '—'}
          delta={selectedRow ? 'selected product' : 'select a product'}
          deltaTone="neutral"
        />
        <KpiTile
          label="Awaiting review"
          value={kpis.awaitingReview.toLocaleString()}
          delta={rowsComplete ? 'all products' : `first ${rows.length} scanned`}
          deltaTone={kpis.awaitingReview > 0 ? 'down' : 'neutral'}
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4" />
          {error}
          <button type="button" onClick={() => void loadRows()} className="ml-auto underline">
            Retry
          </button>
        </div>
      )}

      {reviewQueue.length > 0 && (
        <AonikCard
          title="Awaiting review"
          subtitle="While flagged, these products serve figures captioned and withhold every declaration"
          padding={0}
        >
          <ul className="flex flex-col divide-y divide-[var(--color-border-light)]">
            {reviewQueue.map((row) => (
              <li key={row.productId} className="flex items-center gap-3 px-4 py-2.5">
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate text-[13px] text-[var(--color-text-primary)]">
                    {row.name}
                  </span>
                  <span className="text-[11px] text-[var(--color-text-tertiary)]">
                    {row.requiresReview
                      ? 'The recommended default moved underneath this block'
                      : 'The block describes a combination that is no longer the standard preparation'}
                  </span>
                </span>
                <Button variant="outline" size="sm" onClick={() => setSelectedId(row.productId)}>
                  Open
                </Button>
                <Button size="sm" onClick={() => void confirmReview(row.productId)}>
                  Confirm review
                </Button>
              </li>
            ))}
          </ul>
        </AonikCard>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-10">
          <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[280px_1fr]">
          <AonikCard title="Products" padding={0}>
            {rows.length === 0 ? (
              <p className="px-4 py-8 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                No products to author content for.
              </p>
            ) : (
              <ul className="flex max-h-[560px] flex-col divide-y divide-[var(--color-border-light)] overflow-y-auto">
                {rows.map((row) => {
                  const state = stateOf(row);
                  return (
                    <li key={row.productId}>
                      <button
                        type="button"
                        onClick={() => setSelectedId(row.productId)}
                        className={`flex w-full items-center gap-2 px-4 py-2.5 text-left hover:bg-[var(--color-surface-inset)] ${
                          row.productId === selectedId ? 'bg-[var(--color-surface-inset)]' : ''
                        }`}
                      >
                        <span className="flex min-w-0 flex-1 flex-col">
                          <span className="truncate text-[13px] text-[var(--color-text-primary)]">
                            {row.name}
                          </span>
                          <span className="truncate font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                            {row.slug}
                          </span>
                        </span>
                        <Pill tone={STATE_TONE[state]} size="sm">
                          {STATE_LABEL[state]}
                        </Pill>
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
            {!rowsComplete && (
              <p className="border-t border-[var(--color-border-light)] px-4 py-2 text-[11px] text-[var(--color-text-tertiary)]">
                Showing the first {rows.length} products; the rest were not scanned.
              </p>
            )}
          </AonikCard>

          <div className="flex flex-col gap-4">
            {detailError && (
              <p className="rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                {detailError}
              </p>
            )}

            {detailLoading ? (
              <AonikCard padding={12}>
                <p className="py-8 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                  Loading…
                </p>
              </AonikCard>
            ) : (
              <>
                <ContentWorkbench
                  block={content?.block ?? null}
                  state={selectedState}
                  onAuthor={selectedId ? () => setEditingBlock(true) : undefined}
                  onEdit={selectedId ? () => setEditingBlock(true) : undefined}
                />

                <AonikCard
                  title="Combinations"
                  subtitle="Each describes one complete selection; declarations left empty are withheld for it"
                  padding={0}
                  action={
                    selectedId && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setVariantSheet({ variant: null, selectionJson: null })}
                      >
                        Author a combination
                      </Button>
                    )
                  }
                >
                  {!content || content.variants.length === 0 ? (
                    <p className="px-4 py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                      No combinations authored — every selection resolves to the default block.
                    </p>
                  ) : (
                    <ul className="flex flex-col divide-y divide-[var(--color-border-light)]">
                      {content.variants.map((variant) => (
                        <li key={variant.id} className="flex items-center gap-3 px-4 py-2.5">
                          <span className="min-w-0 flex-1 truncate font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">
                            {variant.selectionJson}
                          </span>
                          <Pill tone={variant.isActive ? 'success' : 'muted'} size="sm">
                            {variant.isActive ? 'Active' : 'Retired'}
                          </Pill>
                          <button
                            type="button"
                            onClick={() => setVariantSheet({ variant, selectionJson: null })}
                            className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                          >
                            Edit
                          </button>
                          {variant.isActive && (
                            <button
                              type="button"
                              onClick={() => void retireVariant(variant.id)}
                              className="text-[11.5px] text-[var(--color-text-secondary)] hover:underline"
                            >
                              Retire
                            </button>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </AonikCard>

                <AonikCard
                  title="Coverage"
                  subtitle="Single-choice deviations from the standard preparation that nothing describes yet"
                  padding={0}
                >
                  {!coverage ? (
                    <p className="px-4 py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                      Coverage could not be read for this product.
                    </p>
                  ) : coverage.singleChoiceGaps.length === 0 ? (
                    <p className="px-4 py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                      No gaps — every single-choice deviation this product offers is described.
                    </p>
                  ) : (
                    <ul className="flex flex-col divide-y divide-[var(--color-border-light)]">
                      {coverage.singleChoiceGaps.map((gap) => (
                        <li
                          key={`${gap.groupKey}-${gap.choiceKey}`}
                          className="flex items-center gap-3 px-4 py-2.5"
                        >
                          <span className="min-w-0 flex-1 text-[12.5px] text-[var(--color-text-primary)]">
                            <span className="font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-tertiary)]">
                              {gap.groupKey}
                            </span>{' '}
                            → {gap.choiceKey}
                          </span>
                          <button
                            type="button"
                            onClick={() =>
                              setVariantSheet({ variant: null, selectionJson: gap.selectionJson })
                            }
                            className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                          >
                            Author
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </AonikCard>
              </>
            )}
          </div>
        </div>
      )}

      {editingBlock && selectedId && selectedRow && (
        <ContentBlockSheet
          key={selectedId}
          productId={selectedId}
          productName={selectedRow.name}
          block={content?.block ?? null}
          onClose={() => setEditingBlock(false)}
          onSaved={() => {
            void loadRows();
            void loadDetail(selectedId);
          }}
        />
      )}

      {variantSheet && selectedId && (
        <ContentVariantSheet
          key={variantSheet.variant?.id ?? variantSheet.selectionJson ?? 'new'}
          productId={selectedId}
          groups={groups}
          variant={variantSheet.variant}
          initialSelectionJson={variantSheet.selectionJson}
          onClose={() => setVariantSheet(null)}
          onSaved={() => {
            void loadRows();
            void loadDetail(selectedId);
          }}
        />
      )}
    </div>
  );

  async function retireVariant(variantId: string) {
    try {
      await commerceContentService.deleteVariant(variantId);
      toast.success('Combination retired — it can be revived by authoring it again');
      if (selectedId) await loadDetail(selectedId);
      await loadRows();
    } catch (err: unknown) {
      toast.error(readMessage(err) || 'The combination could not be retired.');
    }
  }
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
