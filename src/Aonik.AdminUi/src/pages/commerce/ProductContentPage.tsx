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
  ResolvedContentDto,
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

/** Status rows per page. The rail is PAGED — every product must be reachable for authoring. */
const STATUS_PAGE_SIZE = 50;
/** Pages the review scan will walk before admitting it stopped. Bounded, and disclosed. */
const QUEUE_SCAN_PAGES = 20;

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
  const [rowPage, setRowPage] = useState(1);
  const [rowTotal, setRowTotal] = useState(0);
  const [rowsComplete, setRowsComplete] = useState(true);
  /** Set when the product's effective offer could not be read — variants need it. */
  const [groupsError, setGroupsError] = useState(false);
  /** Identity AND metadata together — see the note where the derived values are read. */
  const [selection, setSelection] = useState<ContentStatusRowDto | null>(null);
  const [content, setContent] = useState<AdminProductContentDto | null>(null);
  const [coverage, setCoverage] = useState<ContentCoverageDto | null>(null);
  const [coverageError, setCoverageError] = useState(false);
  const [groups, setGroups] = useState<EffectiveOptionGroupDto[]>([]);
  /** What the resolver serves for the standard selection — not always the block. */
  const [resolved, setResolved] = useState<ResolvedContentDto | null>(null);
  /** The resolution FAILED (as opposed to reporting no content) — the panel may be wrong. */
  const [resolvedError, setResolvedError] = useState(false);
  /** The product is not active, so the storefront resolution cannot be asked at all. */
  const [resolvedUnavailable, setResolvedUnavailable] = useState(false);
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
  /** The live selection, for async work that must not act on a stale closure value. */
  const selectionRef = useRef<ContentStatusRowDto | null>(null);

  const loadRows = useCallback(async () => {
    const requestId = listRequestRef.current + 1;
    listRequestRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const page = await commerceContentService.listContentStatus(rowPage, STATUS_PAGE_SIZE);
      if (listRequestRef.current !== requestId) return;
      const lastPage = Math.max(1, Math.ceil(page.totalCount / STATUS_PAGE_SIZE));
      if (rowPage > lastPage) {
        setRowTotal(page.totalCount);
        setRowPage(lastPage);
        return;
      }
      setRows(page.items);
      setRowTotal(page.totalCount);
      setRowsComplete(page.totalCount <= STATUS_PAGE_SIZE);
      setSelection((current) => {
        // A product selected FROM THE QUEUE may legitimately live on another status page, so
        // "not in this page's rows" is not grounds to move the selection off it. The object
        // carries its own metadata, so it survives even when no list still holds its row.
        if (!current) return page.items[0] ?? null;
        const refreshed = page.items.find((r) => r.productId === current.productId);
        if (refreshed) return refreshed;
        if (queueRef.current.some((r) => r.productId === current.productId)) return current;
        return page.items[0] ?? null;
      });
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
  }, [rowPage]);

  useEffect(() => {
    void loadRows();
  }, [loadRows]);

  // Takes ONLY the id. Slug and status used to arrive from the list row that triggered the
  // load, and both are read authoritatively from the product below — a parameter that can
  // disagree with the value actually used is a defect waiting for a caller to supply it.
  const loadDetail = useCallback(
    async (productId: string) => {
    const requestId = detailRequestRef.current + 1;
    detailRequestRef.current = requestId;
    setDetailLoading(true);
    setDetailError(null);
    // Cleared at the START. Left standing, the previous product's gap count sat under the
    // newly selected product's name for the whole load — a coverage figure attributed to the
    // wrong product is authoring work aimed somewhere real but wrong.
    setCoverage(null);
    setCoverageError(false);
    try {
      // Coverage and the product's effective offer are fetched for the SELECTED product only —
      // per-product reads for the whole rail would be a fan-out on every page load.
      const [admin, cover, product] = await Promise.all([
        commerceContentService.getAdminContent(productId),
        commerceContentService.getCoverage(productId).then(
          (c) => ({ ok: true as const, coverage: c }),
          () => ({ ok: false as const, coverage: null }),
        ),
        // Tracked as a FAILURE rather than folded into "no groups": an unread offer and a
        // product that genuinely offers nothing look identical downstream, and one of them
        // means combination authoring is broken rather than inapplicable.
        commerceCatalogService.getProduct(productId).then(
          (p) => ({ ok: true as const, product: p }),
          () => ({ ok: false as const, product: null }),
        ),
      ]);
      if (detailRequestRef.current !== requestId) return;

      // Status and slug come from the PRODUCT read, which is authoritative, not from the list
      // row this load was called with. A row is a snapshot: another operator activating a
      // product after it was read leaves the row saying Draft, and gating resolution on that
      // shows the raw block under a "not active" notice while live customers are being served
      // something else entirely — figures and allergens included.
      //
      // When that read FAILS the answer is unknown, not "use the row". The row can be wrong in
      // both directions: a Draft→Active transition makes the page skip resolution and caption
      // the raw block as inactive, and a changed slug turns the old route's 404 into a
      // confident "nothing authored" — each presenting the block as what customers receive
      // while an exact variant may be serving different figures and allergens. Failing closed
      // here costs a panel; guessing costs a false statement about live content.
      const resolvedContent = !product.ok
        ? { ok: false as const, content: null, unresolvable: false }
        : await (async () => {
            const liveSlug = product.product?.slug ?? null;
            const liveIsActive = product.product?.status === 'Active';
            // Sequenced after the status is known rather than fired alongside it. The public
            // route 404s for a non-active product before resolving anything, so asking without
            // knowing is how a "not authored" answer gets fabricated for a product that simply
            // was not live yet.
            if (!liveSlug || !liveIsActive) {
              return { ok: true as const, content: null, unresolvable: !liveIsActive };
            }
            return await commerceContentService.resolveContent(liveSlug).then(
              (r) => ({ ok: true as const, content: r, unresolvable: false }),
              (err: unknown) =>
                httpStatus(err) === 404
                  ? { ok: true as const, content: null, unresolvable: false }
                  : { ok: false as const, content: null, unresolvable: false },
            );
          })();
      if (detailRequestRef.current !== requestId) return;
      setContent(admin);
      setCoverage(cover.coverage);
      setCoverageError(!cover.ok);
      setGroups(product.product?.effectiveOptionGroups ?? []);
      setResolved(resolvedContent.content);
      setResolvedError(!resolvedContent.ok);
      setResolvedUnavailable(resolvedContent.unresolvable);
      setGroupsError(!product.ok);
    } catch (err: unknown) {
      if (detailRequestRef.current !== requestId) return;
      setContent(null);
      setCoverage(null);
      setCoverageError(false);
      setGroups([]);
      setResolved(null);
      setResolvedError(false);
      setResolvedUnavailable(false);
      setGroupsError(false);
      setDetailError(readMessage(err) || 'This product’s content could not be read.');
    } finally {
      if (detailRequestRef.current === requestId) setDetailLoading(false);
      }
    },
    [],
  );

  // Scanned across pages rather than derived from the rail's CURRENT page. Paging the rail
  // made every product reachable and, in the same move, hid every flagged product that was not
  // on the page in view — a safety queue that disappears when you turn a page is worse than
  // one that admits a bound.
  const [queue, setQueue] = useState<ContentStatusRowDto[]>([]);
  /** Read inside loadRows, which must not depend on `queue` and re-run on every scan. */
  const queueRef = useRef<ContentStatusRowDto[]>([]);
  /**
   * Generation guard, matching the list and detail loaders.
   *
   * A multi-page scan is slow enough that the one triggered by a block save can finish BEFORE
   * the initial or retried scan it superseded — putting a product whose review flag was just
   * cleared back into the queue, and into the tenant KPI, until some later scan happens to fix
   * it.
   */
  const queueRequestRef = useRef(0);
  const [queueComplete, setQueueComplete] = useState(true);
  const [queueFailed, setQueueFailed] = useState(false);
  /**
   * A scan is IN FLIGHT — distinct from one that finished bounded, and from one that failed.
   *
   * `loadQueue` publishes nothing until it finishes, so deriving "scanning" from the absence of
   * the other two states put an in-flight scan through the label meant for a completed-but-
   * bounded one: "Awaiting review 0 — first 1000 scanned", asserting a count nothing had
   * counted yet. The earlier fix stopped that reading as ALL products; it still read as a
   * finished scan.
   */
  const [queueScanning, setQueueScanning] = useState(true);

  const loadQueue = useCallback(async () => {
    const requestId = queueRequestRef.current + 1;
    queueRequestRef.current = requestId;
    const found: ContentStatusRowDto[] = [];
    /** Every product this scan actually looked at — the only rows it may speak for. */
    const scannedIds: string[] = [];
    setQueueFailed(false);
    setQueueScanning(true);
    // Marked incomplete BEFORE the first await. `queueComplete` starting true meant that while
    // a multi-page scan was still walking, the KPI read "0 — all products" and the card was
    // hidden: a false all-clear produced by an in-flight scan rather than a failed one.
    setQueueComplete(false);
    try {
      let page = 1;
      for (; page <= QUEUE_SCAN_PAGES; page += 1) {
        const result = await commerceContentService.listContentStatus(page, STATUS_PAGE_SIZE);
        scannedIds.push(...result.items.map((r) => r.productId));
        found.push(...result.items.filter((r) => r.isStale));
        if (queueRequestRef.current !== requestId) return;
        if (page * STATUS_PAGE_SIZE >= result.totalCount) {
          setQueue(found);
          queueRef.current = found;
          setQueueComplete(true);
          setQueueScanning(false);
          return;
        }
      }
      setQueue(found);
      queueRef.current = found;
      setQueueComplete(false);
      setQueueScanning(false);
    } catch {
      if (queueRequestRef.current !== requestId) return;
      setQueueScanning(false);
      // PARTIAL results are MERGED with what was already known, not swapped for them. A scan
      // that reaches page 3 and fails has refreshed pages 1-3 and learned nothing about the
      // rest, so replacing the queue with `found` deletes flagged products a previous scan did
      // find — actionable work removed from the operator's list by a failure, which is the
      // opposite of what a failure should do. Rows the partial scan DID revisit are taken from
      // it, so a review cleared in between still disappears.
      setQueue((current) => {
        const scanned = new Set(scannedIds);
        const carried = current.filter((r) => !scanned.has(r.productId));
        const next = [...found, ...carried];
        queueRef.current = next;
        return next;
      });
      setQueueComplete(false);
      setQueueFailed(true);
    }
  }, []);

  useEffect(() => {
    void loadQueue();
  }, [loadQueue]);

  const reviewQueue = queue;

  // The selection is ONE object, captured when the operator picks a product, not four values
  // derived from lists that change underneath. Deriving them separately produced two distinct
  // defects: async work paired a live id with a stale slug, and a successful queue refresh
  // could remove the only row describing the current selection — leaving the id and its loaded
  // content in place while the metadata vanished, so the editor could mount for one product
  // holding another's block.
  const selectedId = selection?.productId ?? null;
  const selectedRow =
    rows.find((r) => r.productId === selectedId) ??
    queue.find((r) => r.productId === selectedId) ??
    selection ??
    null;

  useEffect(() => {
    selectionRef.current = selection;
    if (selection) {
      void loadDetail(selection.productId);
    }
  }, [selection, loadDetail]);



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
      awaitingReview: queue.length,
    };
  }, [rows, queue]);



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
          delta={rowsComplete ? 'active products' : `${rows.length} on this page`}
          deltaTone="neutral"
        />
        <KpiTile
          label="Combination variants"
          value={kpis.variants.toLocaleString()}
          delta={rowsComplete ? 'all products' : `${rows.length} on this page`}
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
          // An in-flight scan has no count to show. Printing 0 while the pages are still being
          // walked is the same false all-clear as before, dressed as a finished figure.
          value={queueScanning ? '—' : kpis.awaitingReview.toLocaleString()}
          delta={
            queueScanning
              ? 'scanning…'
              : queueComplete
                ? 'all products'
                : queueFailed
                  ? 'scan incomplete'
                  : `first ${QUEUE_SCAN_PAGES * STATUS_PAGE_SIZE} scanned`
          }
          deltaTone={!queueScanning && kpis.awaitingReview > 0 ? 'down' : 'neutral'}
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

      {(reviewQueue.length > 0 || queueFailed || queueScanning) && (
        <AonikCard
          title="Awaiting review"
          // NOT "these products withhold their declarations". The queue knows only the BLOCK's
          // status, and when a default moves onto a combination that already has an active
          // variant the resolver serves that variant and withholds nothing. Asserting the
          // withholding here repeated, at tenant scale, the exact error the workbench had.
          subtitle="Each product's default block no longer describes the current standard preparation"
          padding={0}
        >
          {reviewQueue.length === 0 && (queueFailed || queueScanning) && (
            <p className="px-4 py-3 text-[12px] text-[var(--color-text-secondary)]">
              {queueScanning
                ? 'Scanning every product — this list is not complete yet.'
                : 'The review scan did not finish, so this list is not proof that nothing is flagged.'}
            </p>
          )}
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
                    {' — open it to see what customers currently receive'}
                  </span>
                </span>
                <Button variant="outline" size="sm" onClick={() => setSelection(row)}>
                  Open
                </Button>
                {/*
                  Confirming lives in the block EDITOR, not here. This row cannot show the text
                  being confirmed, and neither can the workbench: it prefers the resolved panel,
                  which withholds ingredients and allergens precisely while a block is stale. A
                  button here published unseen declarations — and the operator had no way to
                  inspect them even if they wanted to. Opening the editor is the review.
                */}
                <span className="text-[11px] text-[var(--color-text-tertiary)]">
                  {row.productId === selectedId ? 'edit the block to review it' : 'open it to review'}
                </span>
              </li>
            ))}
          </ul>
          {(!queueComplete || queueScanning) && (
            <p className="flex items-center gap-2 border-t border-[var(--color-border-light)] px-4 py-2 text-[11px] text-[var(--color-text-tertiary)]">
              <span className="flex-1">
                {queueScanning
                  ? 'Still scanning — flagged products may not have been reached yet.'
                  : queueFailed
                    ? 'This scan did not finish, so there may be flagged products it never reached.'
                    : `Scanned the first ${QUEUE_SCAN_PAGES * STATUS_PAGE_SIZE} products; there may be more flagged beyond them.`}
              </span>
              {queueFailed && !queueScanning && (
                <button type="button" onClick={() => void loadQueue()} className="underline">
                  Rescan
                </button>
              )}
            </p>
          )}
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
                        onClick={() => setSelection(row)}
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
            {rowTotal > STATUS_PAGE_SIZE && (
              <div className="flex items-center justify-between gap-2 border-t border-[var(--color-border-light)] px-3 py-2">
                <span className="text-[11px] text-[var(--color-text-tertiary)]">
                  {(rowPage - 1) * STATUS_PAGE_SIZE + 1}–
                  {Math.min(rowPage * STATUS_PAGE_SIZE, rowTotal)} of {rowTotal}
                </span>
                <span className="flex gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={rowPage <= 1}
                    onClick={() => setRowPage((p) => Math.max(1, p - 1))}
                  >
                    Prev
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={rowPage * STATUS_PAGE_SIZE >= rowTotal}
                    onClick={() => setRowPage((p) => p + 1)}
                  >
                    Next
                  </Button>
                </span>
              </div>
            )}
          </AonikCard>

          <div className="flex flex-col gap-4">
            {detailError && (
              <p className="flex items-center gap-2 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                {/*
                  Retry lives HERE because the shared catch clears every other piece of detail
                  state, so the resolution, offer and coverage retries are all unmounted when
                  this fires. Re-clicking the rail row usually hands `setSelection` the same row
                  object, which React treats as no change — so without this the operator has to
                  navigate to another product, or reload the page, before the block they were
                  asked to review can be read at all.
                */}
                <span className="flex-1">{detailError}</span>
                <button
                  type="button"
                  onClick={() => void reloadSelected()}
                  disabled={detailLoading}
                  className="shrink-0 underline"
                >
                  {detailLoading ? 'Retrying…' : 'Retry'}
                </button>
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
                {resolvedUnavailable && (
                  <p className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface-inset)] px-3 py-2 text-[12px] text-[var(--color-text-secondary)]">
                    This product is not active, so the storefront cannot resolve what it would
                    serve. The panel below is the stored block — once the product is active, an
                    authored combination may serve instead.
                  </p>
                )}

                {resolvedError && (
                  <p className="flex items-center gap-2 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-[12px] text-[var(--color-warning)]">
                    <span className="flex-1">
                      What customers currently receive could not be read, so the panel below
                      shows the stored block — which is not always what is served.
                    </span>
                    <button
                      type="button"
                      onClick={() => void reloadSelected()}
                      className="shrink-0 underline"
                    >
                      Retry
                    </button>
                  </p>
                )}

                <ContentWorkbench
                  block={content?.block ?? null}
                  resolved={resolved}
                  state={selectedState}
                  // Authoring is gated on the RAW read having succeeded. Without it a product
                  // the status row says HAS a block renders as "Nothing published", and the
                  // CTA opens a blank sheet over content nobody has seen.
                  onAuthor={selectedId && content ? () => setEditingBlock(true) : undefined}
                  onEdit={selectedId && content ? () => setEditingBlock(true) : undefined}
                />

                <AonikCard
                  title="Combinations"
                  subtitle="Each describes one complete selection; declarations left empty are withheld for it"
                  padding={0}
                  action={
                    // Hidden when the product offers nothing to combine, and when the offer
                    // could not be READ — the sheet would open a workflow whose every save
                    // fails, for two quite different reasons.
                    selectedId &&
                    groups.length > 0 &&
                    // V-C8: the default block is the baseline every variant validates against,
                    // so AddVariantAsync rejects the whole sheet without one. Authoring the
                    // block first is the prerequisite, not a preference.
                    !!content?.block && (
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
                  {selectedId && groups.length > 0 && !content?.block && (
                    <p className="mx-3 mt-3 rounded-md border border-[var(--color-border)] bg-[var(--color-surface-inset)] px-3 py-2 text-[12px] text-[var(--color-text-secondary)]">
                      Author the default block first — it is the baseline every combination is
                      validated against, so one cannot be saved without it.
                    </p>
                  )}
                  {groupsError && (
                    <p className="mx-3 mt-3 flex items-center gap-2 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-[12px] text-[var(--color-warning)]">
                      <span className="flex-1">
                        This product’s option offer could not be read, so combinations cannot be
                        authored right now. Existing ones are still listed.
                      </span>
                      <button
                        type="button"
                        onClick={() => void reloadSelected()}
                        className="shrink-0 underline"
                      >
                        Retry
                      </button>
                    </p>
                  )}
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
                          {variant.isActive ? (
                            <>
                              <button
                                type="button"
                                onClick={() => setVariantSheet({ variant, selectionJson: null })}
                                className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                              >
                                Edit
                              </button>
                              <button
                                type="button"
                                onClick={() => void retireVariant(variant.id)}
                                className="text-[11.5px] text-[var(--color-text-secondary)] hover:underline"
                              >
                                Retire
                              </button>
                            </>
                          ) : (
                            // A retired row is history: UpdateVariantAsync rejects every edit
                            // to it with V-C5. Re-authoring the SAME combination is the
                            // supported path and revives the row, so that is what is offered.
                            groups.length > 0 &&
                            !!content?.block &&
                            // V-C1 rejects any canonical selection equal to the current
                            // defaults, so a retired variant whose combination has SINCE become
                            // the standard preparation cannot be revived at all — the default
                            // block is where that content belongs now.
                            //
                            // Compared against the ADMIN read's binding, not the public
                            // resolution. Both answer "what is the standard preparation", but
                            // only one of them exists for a Draft or Archived product — gating
                            // on the resolution told operators to ACTIVATE the product before
                            // restoring its content, which is advice to publish an incomplete
                            // storefront to fix an authoring problem. The same false gate hit an
                            // active product whose resolution read merely failed.
                            variant.selectionJson !== content.currentDefaultsSelectionJson ? (
                              <button
                                type="button"
                                onClick={() =>
                                  setVariantSheet({
                                    variant: null,
                                    selectionJson: variant.selectionJson,
                                  })
                                }
                                className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                              >
                                Re-author to revive
                              </button>
                            ) : (
                              <span className="text-[11px] text-[var(--color-text-tertiary)]">
                                {variant.selectionJson === content?.currentDefaultsSelectionJson
                                  ? 'now the standard — edit the block'
                                  : 'retired'}
                              </span>
                            )
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
                    <p className="flex items-center justify-center gap-2 px-4 py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                      <span>
                        {coverageError
                          ? 'Coverage could not be read, so gaps are unknown for this product.'
                          : 'No coverage information for this product.'}
                      </span>
                      {coverageError && (
                        <button
                          type="button"
                          onClick={() =>
                            void reloadSelected()
                          }
                          className="underline"
                        >
                          Retry
                        </button>
                      )}
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
                          {/* Same two gates as the card's CTA: a readable offer, and a default
                              block to validate against. Without either, the sheet opens a
                              workflow whose every save is rejected. */}
                          {groups.length > 0 && !!content?.block ? (
                            <button
                              type="button"
                              onClick={() =>
                                setVariantSheet({ variant: null, selectionJson: gap.selectionJson })
                              }
                              className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                            >
                              Author
                            </button>
                          ) : (
                            <span className="text-[11px] text-[var(--color-text-tertiary)]">
                              {content?.block ? 'offer unread' : 'needs a default block'}
                            </span>
                          )}
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
          expectedDefaults={content?.currentDefaultsSelectionJson ?? ''}
          groups={groups}
          isStale={!!content?.isStale}
          onClose={() => setEditingBlock(false)}
          onSaved={() => {
            void loadRows();
            // Saving OR confirming clears the review flag, so the queue and its KPI are stale
            // the moment either happens.
            void loadQueue();
            void reloadSelected();
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
          expectedDefaults={content?.currentDefaultsSelectionJson ?? ''}
          onClose={() => setVariantSheet(null)}
          onSaved={() => {
            void loadRows();
            void reloadSelected();
          }}
        />
      )}
    </div>
  );

  /**
   * Reload whatever is selected NOW.
   *
   * Every reload that follows an await must read the live selection rather than the values its
   * closure captured. A reload fired for a product the operator has since navigated away from
   * starts last, so it WINS the detail request counter and leaves `content` describing one
   * product while the page believes another is selected — the state the block editor seeds a
   * full-replacement form from.
   */
  async function reloadSelected() {
    const live = selectionRef.current;
    if (live) await loadDetail(live.productId);
  }

  async function retireVariant(variantId: string) {
    try {
      await commerceContentService.deleteVariant(variantId);
      toast.success('Combination retired — it can be revived by authoring it again');
      await reloadSelected();
      await loadRows();
    } catch (err: unknown) {
      toast.error(readMessage(err) || 'The combination could not be retired.');
    }
  }
}

/** The HTTP status of a rejected api call, or undefined for a transport-level failure. */
function httpStatus(err: unknown): number | undefined {
  if (!err || typeof err !== 'object' || !('response' in err)) return undefined;
  const response = (err as { response?: { status?: number } }).response;
  return typeof response?.status === 'number' ? response.status : undefined;
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
